using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Services;

public sealed class CreditStatementGenerationService(
    ICreditStatementRepository statementRepository,
    ICreditRepository creditRepository) : ICreditStatementGenerationService
{
    public async Task<Result<CreditStatementDto>> GenerateAsync(
        Guid restaurantId, int year, int month, CancellationToken ct)
    {
        var (periodStart, periodEnd) = CreditStatementPeriodCalculator.ResolvePeriod(year, month);

        if (periodEnd > DateTime.UtcNow)
            return Result<CreditStatementDto>.Failure(Error.Validation(
                "STATEMENT_PERIOD_NOT_CLOSED",
                "A statement can only be generated once its billing period has fully elapsed."));

        // Idempotent — regenerating an already-generated period returns the existing
        // statement rather than creating a duplicate or erroring.
        var existing = await statementRepository.FindByPeriodAsync(restaurantId, periodStart, ct);
        if (existing is not null)
            return Result<CreditStatementDto>.Success(CreditStatementDtoMapper.ToDto(existing));

        var statement = await BuildStatementAsync(restaurantId, periodStart, periodEnd, ct);
        await statementRepository.AddAsync(statement, ct);

        try
        {
            await statementRepository.SaveChangesAsync(ct);
        }
        catch (CreditStatementConcurrencyException)
        {
            // A concurrent request generated the same period first — return what it
            // created instead of surfacing a conflict for what is, from the caller's
            // point of view, a successful (idempotent) generate.
            var raced = await statementRepository.FindByPeriodAsync(restaurantId, periodStart, ct);
            return raced is not null
                ? Result<CreditStatementDto>.Success(CreditStatementDtoMapper.ToDto(raced))
                : Result<CreditStatementDto>.Failure(Error.Conflict(
                    "STATEMENT_GENERATION_CONFLICT",
                    "The statement could not be generated due to a concurrent request."));
        }

        return Result<CreditStatementDto>.Success(CreditStatementDtoMapper.ToDto(statement));
    }

    private async Task<CreditStatement> BuildStatementAsync(
        Guid restaurantId, DateTime periodStart, DateTime periodEnd, CancellationToken ct)
    {
        // Opening balance ALWAYS comes from the ledger (DEC-CRE-06), never from a prior
        // statement's ClosingBalance. A statement-chaining shortcut is unsafe: if a period
        // is skipped (the monthly job misses a month, or on-demand generate runs
        // out of order per DEC-CRE-05), the most recent prior statement is not adjacent to
        // this one, and carrying its balance forward would silently drop the skipped
        // period's ledger movements from this snapshot — permanently, since statements are
        // immutable once generated. GetNetBalanceMovementBeforeAsync is immune to this: it
        // is mathematically equivalent to "the account's OutstandingBalance at PeriodStart"
        // (Charge/Settlement/Refund are the only types that ever move the balance, which
        // starts at 0), regardless of which statements have or haven't been generated.
        var openingBalance = await creditRepository.GetNetBalanceMovementBeforeAsync(restaurantId, periodStart, ct);

        var transactions = await creditRepository.GetTransactionsInPeriodAsync(
            restaurantId, periodStart, periodEnd, ct);

        var totalCharges = SumByType(transactions, CreditTransactionType.Charge);
        var totalSettlements = SumByType(transactions, CreditTransactionType.Settlement);
        var totalRefunds = SumByType(transactions, CreditTransactionType.Refund);

        var lines = transactions
            .Select(t => new CreditStatementLine(
                t.Id, t.Type, t.Amount, t.BalanceAfter, t.CreatedAt, t.Note, t.Reference))
            .ToList()
            .AsReadOnly();

        return new CreditStatement(
            restaurantId, periodStart, periodEnd, openingBalance, totalCharges, totalSettlements, totalRefunds, lines);
    }

    private static decimal SumByType(IReadOnlyList<CreditTransaction> transactions, CreditTransactionType type) =>
        transactions.Where(t => t.Type == type).Sum(t => t.Amount);
}

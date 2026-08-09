using FreshFlow.Contracts;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Orders.Application.Services;

public sealed class CreditStatementGenerationService(
    ICreditStatementRepository statementRepository,
    ICreditRepository creditRepository,
    IStatementPdfRenderer pdfRenderer,
    IPublisher publisher,
    ILogger<CreditStatementGenerationService> logger) : ICreditStatementGenerationService
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

        var dto = CreditStatementDtoMapper.ToDto(statement);
        var (pdfBytes, pdfFileName) = RenderPdfOrNull(dto);

        // Published only here — the newly-generated path — so an idempotent re-generate or a
        // race loser (both returned above) never re-notifies. Fires after the commit
        // succeeded, so the notification only goes out for a statement that actually exists.
        try
        {
            await publisher.Publish(
                new CreditStatementGeneratedIntegrationEvent(
                    statement.RestaurantId,
                    statement.Id,
                    statement.PeriodStart,
                    statement.PeriodEnd,
                    statement.ClosingBalance,
                    CreditStatementPeriodCalculator.ResolveDueDate(statement.PeriodEnd),
                    statement.GeneratedAt,
                    pdfBytes,
                    pdfFileName),
                ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // ponytail: use an outbox if statement notifications become guaranteed delivery.
            logger.LogError(
                ex,
                "Failed to publish statement notification for StatementId={StatementId} after it was committed.",
                statement.Id);
        }

        return Result<CreditStatementDto>.Success(dto);
    }

    // PDF rendering is best-effort: a render failure must never block statement generation or
    // its notification — it only means the email goes out without an attachment.
    private (byte[]? Bytes, string? FileName) RenderPdfOrNull(CreditStatementDto statement)
    {
        try
        {
            var bytes = pdfRenderer.Render(statement);
            // PeriodStart is stored UTC but represents a VN local month boundary (DEC-CRE-03) —
            // convert back to VN before labelling the file, or a period starting "May 31 17:00
            // UTC" (= June 1 VN) would wrongly be named for May.
            var periodStartLocal = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(statement.PeriodStart, DateTimeKind.Utc),
                CreditStatementPeriodCalculator.VietnamTimeZone);
            return (bytes, $"statement-{periodStartLocal:yyyy-MM}.pdf");
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex, "Failed to render statement PDF for StatementId={StatementId}.", statement.Id);
            return (null, null);
        }
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
                t.Id, t.Type, t.Amount, t.BalanceAfter, t.CreatedAt, t.Note, t.Reference,
                t.OrderId, t.PaymentMethod))
            .ToList()
            .AsReadOnly();

        return new CreditStatement(
            restaurantId, periodStart, periodEnd, openingBalance, totalCharges, totalSettlements, totalRefunds, lines);
    }

    private static decimal SumByType(IReadOnlyList<CreditTransaction> transactions, CreditTransactionType type) =>
        transactions.Where(t => t.Type == type).Sum(t => t.Amount);
}

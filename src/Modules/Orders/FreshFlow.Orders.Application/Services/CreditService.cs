using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Services;

public sealed class CreditService(
    ICreditRepository creditRepository,
    IRestaurantReader restaurantReader) : ICreditService
{
    public async Task<Result<CreditCheckDto>> CanChargeAsync(
        Guid restaurantId, decimal amount, CancellationToken ct)
    {
        if (amount <= 0m)
            return Result<CreditCheckDto>.Failure(InvalidAmount());

        var accountResult = await GetAccountOrDefaultAsync(restaurantId, ct);
        if (accountResult.IsFailure)
            return Result<CreditCheckDto>.Failure(accountResult.Error);

        var account = accountResult.Value;
        if (!account.CanCharge(amount))
            return Result<CreditCheckDto>.Failure(CreditLimitExceeded(account, amount));

        return Result<CreditCheckDto>.Success(CreditDtoMapper.ToCheckDto(account, amount));
    }

    public async Task<Result<RestaurantCreditDto>> ChargeAsync(
        Guid restaurantId,
        Guid orderId,
        decimal amount,
        string? note,
        CancellationToken ct)
    {
        if (amount <= 0m)
            return Result<RestaurantCreditDto>.Failure(InvalidAmount());

        var accountResult = await GetAccountOrDefaultAsync(restaurantId, ct);
        if (accountResult.IsFailure)
            return Result<RestaurantCreditDto>.Failure(accountResult.Error);

        var account = accountResult.Value;
        if (!account.CanCharge(amount))
            return Result<RestaurantCreditDto>.Failure(CreditLimitExceeded(account, amount));

        account.Charge(amount);
        await EnsureTrackedAccountAsync(account, ct);
        creditRepository.Track(account);
        creditRepository.AddTransaction(new CreditTransaction(
            restaurantId,
            orderId,
            CreditTransactionType.Charge,
            amount,
            account.OutstandingBalance,
            note));

        return await SaveAndReturnAsync(account, ct);
    }

    public async Task<Result<RestaurantCreditDto>> RefundAsync(
        Guid restaurantId,
        Guid orderId,
        decimal amount,
        string? note,
        CancellationToken ct)
    {
        if (amount <= 0m)
            return Result<RestaurantCreditDto>.Failure(InvalidAmount());

        var accountResult = await GetAccountOrDefaultAsync(restaurantId, ct);
        if (accountResult.IsFailure)
            return Result<RestaurantCreditDto>.Failure(accountResult.Error);

        var account = accountResult.Value;
        if (amount > account.OutstandingBalance)
            return Result<RestaurantCreditDto>.Failure(CreditBalanceExceeded(
                "CREDIT_REFUND_EXCEEDS_BALANCE",
                "Refund amount cannot exceed outstanding balance.",
                account,
                amount));

        account.Refund(amount);
        await EnsureTrackedAccountAsync(account, ct);
        creditRepository.Track(account);
        creditRepository.AddTransaction(new CreditTransaction(
            restaurantId,
            orderId,
            CreditTransactionType.Refund,
            amount,
            account.OutstandingBalance,
            note));

        return await SaveAndReturnAsync(account, ct);
    }

    public async Task<Result<RestaurantCreditDto>> SettleAsync(
        Guid restaurantId,
        decimal amount,
        string? note,
        CancellationToken ct)
    {
        if (amount <= 0m)
            return Result<RestaurantCreditDto>.Failure(InvalidAmount());

        var accountResult = await GetAccountOrDefaultAsync(restaurantId, ct);
        if (accountResult.IsFailure)
            return Result<RestaurantCreditDto>.Failure(accountResult.Error);

        var account = accountResult.Value;
        if (amount > account.OutstandingBalance)
            return Result<RestaurantCreditDto>.Failure(CreditBalanceExceeded(
                "CREDIT_SETTLEMENT_EXCEEDS_BALANCE",
                "Settlement amount cannot exceed outstanding balance.",
                account,
                amount));

        account.Settle(amount);
        await EnsureTrackedAccountAsync(account, ct);
        creditRepository.Track(account);
        creditRepository.AddTransaction(new CreditTransaction(
            restaurantId,
            orderId: null,
            CreditTransactionType.Settlement,
            amount,
            account.OutstandingBalance,
            note));

        return await SaveAndReturnAsync(account, ct);
    }

    private async Task<Result<RestaurantCredit>> GetAccountOrDefaultAsync(Guid restaurantId, CancellationToken ct)
    {
        var restaurant = await restaurantReader.FindByIdAsync(restaurantId, ct);
        if (restaurant is null)
            return Result<RestaurantCredit>.Failure(Error.NotFound("Restaurant", restaurantId));

        var account = await creditRepository.FindAccountAsync(restaurantId, ct);
        if (account is not null)
            return Result<RestaurantCredit>.Success(account);

        return Result<RestaurantCredit>.Success(new RestaurantCredit(restaurantId));
    }

    private async Task EnsureTrackedAccountAsync(RestaurantCredit account, CancellationToken ct)
    {
        var existing = await creditRepository.FindAccountAsync(account.RestaurantId, ct);
        if (existing is null)
            await creditRepository.AddAccountAsync(account, ct);
    }

    private async Task<Result<RestaurantCreditDto>> SaveAndReturnAsync(RestaurantCredit account, CancellationToken ct)
    {
        try
        {
            await creditRepository.SaveChangesAsync(ct);
            return Result<RestaurantCreditDto>.Success(CreditDtoMapper.ToDto(account));
        }
        catch (CreditConcurrencyException)
        {
            return Result<RestaurantCreditDto>.Failure(Error.Conflict(
                "OPTIMISTIC_CONCURRENCY_CONFLICT",
                "The credit account was updated by another request. Please refresh and retry."));
        }
    }

    private static Error InvalidAmount() =>
        Error.Validation("INVALID_AMOUNT", "Amount must be greater than zero.");

    private static Error CreditLimitExceeded(RestaurantCredit account, decimal amount) =>
        Error.Validation(
            "CREDIT_LIMIT_EXCEEDED",
            $"Requested amount {amount} exceeds available credit {account.AvailableCredit}.");

    private static Error CreditBalanceExceeded(
        string code,
        string message,
        RestaurantCredit account,
        decimal amount) =>
        Error.Validation(
            code,
            $"{message} Requested amount {amount}; outstanding balance {account.OutstandingBalance}.");
}

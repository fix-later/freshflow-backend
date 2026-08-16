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

    public async Task<Result<CreditRefundDto>> RefundAsync(
        Guid restaurantId,
        Guid orderId,
        decimal amount,
        string? note,
        CancellationToken ct)
    {
        if (amount <= 0m)
            return Result<CreditRefundDto>.Failure(InvalidAmount());

        var accountResult = await GetAccountOrDefaultAsync(restaurantId, ct);
        if (accountResult.IsFailure)
            return Result<CreditRefundDto>.Failure(accountResult.Error);

        var refundableAmount = await creditRepository.GetRefundableAmountForOrderAsync(orderId, ct);
        if (amount > refundableAmount)
            return Result<CreditRefundDto>.Failure(Error.Validation(
                "CREDIT_REFUND_EXCEEDS_ORDER_CHARGE",
                $"Refund amount cannot exceed the order's remaining charged amount. "
                + $"Requested amount {amount}; refundable amount {refundableAmount}."));

        var account = accountResult.Value;
        if (amount > account.OutstandingBalance)
            return Result<CreditRefundDto>.Failure(CreditBalanceExceeded(
                "CREDIT_REFUND_EXCEEDS_BALANCE",
                "Refund amount cannot exceed outstanding balance.",
                account,
                amount));

        account.Refund(amount);
        await EnsureTrackedAccountAsync(account, ct);
        creditRepository.Track(account);
        var transaction = new CreditTransaction(
            restaurantId,
            orderId,
            CreditTransactionType.Refund,
            amount,
            account.OutstandingBalance,
            note);
        creditRepository.AddTransaction(transaction);

        var saved = await SaveAndReturnAsync(account, ct);
        return saved.IsFailure
            ? Result<CreditRefundDto>.Failure(saved.Error)
            : Result<CreditRefundDto>.Success(
                new CreditRefundDto(transaction.Id, saved.Value));
    }

    public async Task<Result<RestaurantCreditDto>> SettleAsync(
        Guid restaurantId,
        Guid recordedByUserId,
        decimal amount,
        PaymentMethod paymentMethod,
        string? reference,
        string? note,
        CancellationToken ct)
    {
        if (amount <= 0m)
            return Result<RestaurantCreditDto>.Failure(InvalidAmount());

        if (recordedByUserId == Guid.Empty || string.IsNullOrWhiteSpace(reference))
            return Result<RestaurantCreditDto>.Failure(Error.Validation(
                "INVALID_SETTLEMENT_DETAILS", "Recorder and reference are required."));

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
            note,
            paymentMethod,
            reference,
            recordedByUserId));

        return await SaveAndReturnAsync(account, ct);
    }

    public async Task<Result<RestaurantCreditDto>> SetCreditLimitAsync(
        Guid restaurantId,
        decimal newLimit,
        string? note,
        CancellationToken ct)
    {
        if (newLimit < 0m)
            return Result<RestaurantCreditDto>.Failure(InvalidCreditLimit());

        var accountResult = await GetAccountOrDefaultAsync(restaurantId, ct);
        if (accountResult.IsFailure)
            return Result<RestaurantCreditDto>.Failure(accountResult.Error);

        var account = accountResult.Value;
        var previousLimit = account.CreditLimit;
        if (previousLimit == newLimit)
            return Result<RestaurantCreditDto>.Success(CreditDtoMapper.ToDto(account));

        try
        {
            account.SetCreditLimit(newLimit);
        }
        catch (InvalidOperationException)
        {
            return Result<RestaurantCreditDto>.Failure(CreditLimitBelowOutstandingBalance(account, newLimit));
        }

        await EnsureTrackedAccountAsync(account, ct);
        creditRepository.Track(account);

        // A credit-limit change is not a balance movement — no ledger entry is written.
        // The balance ledger (credit_transactions) records only charges, settlements,
        // and refunds; CreditTransactionType.Adjustment is retained for backward
        // compatibility with historical rows only.
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
        catch (DuplicateCreditSettlementException)
        {
            return Result<RestaurantCreditDto>.Failure(Error.Conflict(
                "CREDIT_SETTLEMENT_DUPLICATE_REFERENCE",
                "A settlement with this reference already exists for the restaurant."));
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

    private static Error InvalidCreditLimit() =>
        Error.Validation("INVALID_CREDIT_LIMIT", "Credit limit must be non-negative.");

    private static Error CreditLimitBelowOutstandingBalance(RestaurantCredit account, decimal newLimit) =>
        Error.Validation(
            "CREDIT_LIMIT_BELOW_OUTSTANDING_BALANCE",
            $"New limit {newLimit} cannot be below the outstanding balance {account.OutstandingBalance}.");
}

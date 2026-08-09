using FreshFlow.Orders.Domain.Entities;

namespace FreshFlow.Orders.Application.Dtos;

internal static class CreditDtoMapper
{
    public static RestaurantCreditDto ToDto(RestaurantCredit account) =>
        new(
            account.RestaurantId,
            account.CreditLimit,
            account.OutstandingBalance,
            account.AvailableCredit,
            account.UpdatedAt);

    public static CreditCheckDto ToCheckDto(RestaurantCredit account, decimal requestedAmount) =>
        new(
            account.RestaurantId,
            account.CreditLimit,
            account.OutstandingBalance,
            account.AvailableCredit,
            requestedAmount,
            account.CanCharge(requestedAmount));

    public static CreditTransactionDto ToDto(CreditTransaction transaction) =>
        new(
            transaction.Id,
            transaction.RestaurantId,
            transaction.OrderId,
            ToSnakeCase(transaction.Type.ToString()),
            transaction.Amount,
            transaction.BalanceAfter,
            transaction.Note,
            transaction.PaymentMethod is null ? null : ToSnakeCase(transaction.PaymentMethod.Value.ToString()),
            transaction.Reference,
            transaction.RecordedByUserId,
            transaction.CreatedAt);

    private static string ToSnakeCase(string value) =>
        string.Concat(value.Select((ch, index) =>
            index > 0 && char.IsUpper(ch)
                ? "_" + char.ToLowerInvariant(ch)
                : char.ToLowerInvariant(ch).ToString()));
}

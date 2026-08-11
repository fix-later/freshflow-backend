namespace FreshFlow.Orders.Application.Dtos;

public sealed record CreditTransactionDto(
    Guid Id,
    Guid RestaurantId,
    Guid? OrderId,
    string Type,
    decimal Amount,
    decimal BalanceAfter,
    string? Note,
    string? PaymentMethod,
    string? Reference,
    Guid? RecordedByUserId,
    DateTime CreatedAt);

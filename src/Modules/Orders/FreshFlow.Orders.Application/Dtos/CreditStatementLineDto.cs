namespace FreshFlow.Orders.Application.Dtos;

public sealed record CreditStatementLineDto(
    Guid TransactionId,
    string Type,
    decimal Amount,
    decimal BalanceAfter,
    DateTime OccurredAt,
    string? Note,
    string? Reference,
    Guid? OrderId,
    string? PaymentMethod);

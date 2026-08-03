namespace FreshFlow.Orders.Application.Dtos;

public sealed record OrderClaimDto(
    Guid ClaimId,
    Guid OrderId,
    Guid RestaurantId,
    decimal Amount,
    string Reason,
    string Status,
    Guid CreatedBy,
    DateTime CreatedAt,
    Guid? ReviewedBy,
    DateTime? ReviewedAt,
    string? DecisionNote,
    Guid? RefundTransactionId,
    DateTime UpdatedAt);

public sealed record OrderClaimPageDto(
    IReadOnlyList<OrderClaimDto> Items,
    int PageSize,
    string? NextCursor);

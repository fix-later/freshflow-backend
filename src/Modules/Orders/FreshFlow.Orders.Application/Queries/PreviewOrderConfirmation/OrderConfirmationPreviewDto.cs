namespace FreshFlow.Orders.Application.Queries.PreviewOrderConfirmation;

/// <summary>
/// Read-only preview of whether an order would confirm successfully, without mutating it.
/// Unlike the confirm path, <see cref="Issues"/> lists every blocking reason rather than
/// short-circuiting on the first one.
/// </summary>
public sealed record OrderConfirmationPreviewDto(
    bool WouldSucceed,
    IReadOnlyList<PreviewIssueDto> Issues,
    decimal TotalAmount,
    DateTime? ResolvedScheduledFor,
    decimal? RemainingCreditAfter,
    decimal SubtotalAmount = 0m,
    decimal VatAmount = 0m,
    decimal DeliveryFee = 0m,
    decimal DeliveryDistanceKm = 0m);

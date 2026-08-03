using FreshFlow.Orders.Domain.Entities;

namespace FreshFlow.Orders.Application.Dtos;

internal static class OrderClaimDtoMapper
{
    public static OrderClaimDto ToDto(OrderClaim claim) =>
        new(
            claim.Id,
            claim.OrderId,
            claim.RestaurantId,
            claim.Amount,
            claim.Reason,
            claim.Status.ToString().ToLowerInvariant(),
            claim.CreatedBy,
            claim.CreatedAt,
            claim.ReviewedBy,
            claim.ReviewedAt,
            claim.DecisionNote,
            claim.RefundTransactionId,
            claim.UpdatedAt);
}

namespace FreshFlow.Auth.Application.Queries.GetRestaurantApprovalStatus;

public sealed record GetRestaurantApprovalStatusResponse(
    Guid RestaurantId,
    string Status,
    DateTime UpdatedAt);

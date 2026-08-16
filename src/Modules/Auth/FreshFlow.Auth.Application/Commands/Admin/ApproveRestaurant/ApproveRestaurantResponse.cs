namespace FreshFlow.Auth.Application.Commands.Admin.ApproveRestaurant;

public sealed record ApproveRestaurantResponse(
    Guid RestaurantId,
    string RestaurantName,
    bool IsApproved,
    DateTime UpdatedAt);

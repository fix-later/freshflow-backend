namespace FreshFlow.Auth.Application.Commands.Admin.ReactivateRestaurant;

public sealed record ReactivateRestaurantResponse(
    Guid RestaurantId,
    string RestaurantName,
    bool IsActive,
    DateTime UpdatedAt);

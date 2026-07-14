namespace FreshFlow.Auth.Application.Commands.Admin.SuspendRestaurant;

public sealed record SuspendRestaurantResponse(
    Guid RestaurantId,
    string RestaurantName,
    bool IsSuspended,
    DateTime UpdatedAt);

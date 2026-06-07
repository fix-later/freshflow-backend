namespace FreshFlow.Auth.Application.Commands.RegisterRestaurant;

public sealed record RegisterRestaurantResponse(
    Guid UserId,
    Guid RestaurantId,
    string Email,
    string RestaurantName,
    bool IsApproved);

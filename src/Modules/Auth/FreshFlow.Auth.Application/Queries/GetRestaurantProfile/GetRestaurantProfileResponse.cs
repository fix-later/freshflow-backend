namespace FreshFlow.Auth.Application.Queries.GetRestaurantProfile;

public sealed record GetRestaurantProfileResponse(
    Guid RestaurantId,
    string Name,
    string Status,
    string? Address,
    string? ContactPerson,
    TimeOnly? PickupStart,
    TimeOnly? PickupEnd,
    DateTime UpdatedAt);

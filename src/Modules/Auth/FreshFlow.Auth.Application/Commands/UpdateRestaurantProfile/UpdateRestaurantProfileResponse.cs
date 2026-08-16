namespace FreshFlow.Auth.Application.Commands.UpdateRestaurantProfile;

public sealed record UpdateRestaurantProfileResponse(
    Guid RestaurantId,
    string Name,
    string? Address,
    string? ContactPerson,
    TimeOnly? PickupStart,
    TimeOnly? PickupEnd,
    DateTime UpdatedAt,
    string? BusinessLicenseUrl = null);

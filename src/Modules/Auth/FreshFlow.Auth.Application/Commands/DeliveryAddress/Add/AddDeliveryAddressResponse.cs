namespace FreshFlow.Auth.Application.Commands.DeliveryAddress.Add;

public sealed record AddDeliveryAddressResponse(
    Guid Id,
    Guid RestaurantId,
    string? RecipientName,
    string? Phone,
    string AddressLine,
    decimal? Latitude,
    decimal? Longitude,
    bool IsDefault,
    DateTime CreatedAt,
    DateTime UpdatedAt);

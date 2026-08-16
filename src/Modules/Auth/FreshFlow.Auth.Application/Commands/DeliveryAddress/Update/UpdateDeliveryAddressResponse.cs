namespace FreshFlow.Auth.Application.Commands.DeliveryAddress.Update;

public sealed record UpdateDeliveryAddressResponse(
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

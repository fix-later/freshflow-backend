namespace FreshFlow.Auth.Application.Abstractions;

public interface IDeliveryAddressRepository
{
    public Task<DeliveryAddressDto> AddAsync(
        Guid restaurantId,
        string? recipientName,
        string? phone,
        string addressLine,
        decimal? latitude,
        decimal? longitude,
        bool isDefault,
        CancellationToken ct);

    public Task<IReadOnlyList<DeliveryAddressDto>> GetByRestaurantIdAsync(
        Guid restaurantId, CancellationToken ct);

    public Task<DeliveryAddressDto?> FindByIdAndRestaurantIdAsync(
        Guid addressId, Guid restaurantId, CancellationToken ct);

    public Task<DeliveryAddressDto> UpdateAsync(
        Guid addressId,
        string? recipientName,
        string? phone,
        string addressLine,
        decimal? latitude,
        decimal? longitude,
        bool isDefault,
        CancellationToken ct);

    public Task SoftDeleteAsync(Guid addressId, CancellationToken ct);

    public Task ClearDefaultsAsync(Guid restaurantId, CancellationToken ct);
}

public sealed record DeliveryAddressDto(
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

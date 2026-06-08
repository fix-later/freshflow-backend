namespace FreshFlow.Auth.Application.Abstractions;

public interface IRestaurantRepository
{
    public Task<Guid> CreateAsync(Guid userId, string restaurantName, CancellationToken ct);
    public Task<RestaurantDto?> FindByIdAsync(Guid restaurantId, CancellationToken ct);
    public Task<RestaurantDto?> FindByUserIdAsync(Guid userId, CancellationToken ct);
    public Task<bool> ApproveAsync(Guid restaurantId, CancellationToken ct);
    public Task<RestaurantDto> UpdateProfileAsync(
        Guid restaurantId,
        string name,
        string? address,
        string? contactPerson,
        TimeOnly? pickupStart,
        TimeOnly? pickupEnd,
        CancellationToken ct);
}

public sealed record RestaurantDto(
    Guid Id,
    string Name,
    bool IsApproved,
    DateTime UpdatedAt,
    Guid UserId,
    string? Address = null,
    string? ContactPerson = null,
    TimeOnly? PickupStart = null,
    TimeOnly? PickupEnd = null);

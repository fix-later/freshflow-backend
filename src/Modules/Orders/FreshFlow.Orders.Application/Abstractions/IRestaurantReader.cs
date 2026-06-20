namespace FreshFlow.Orders.Application.Abstractions;

/// <summary>
/// Cross-module read service for resolving the restaurant tied to the authenticated user
/// and checking its approval status. Implemented in Infrastructure via a read-only
/// projection — Orders has no project reference to Auth.
/// </summary>
public interface IRestaurantReader
{
    /// <summary>Returns the restaurant owned by <paramref name="userId"/>, or null if none exists.</summary>
    public Task<RestaurantSnapshotDto?> FindByUserIdAsync(Guid userId, CancellationToken ct);

    /// <summary>Returns a restaurant by ID, or null if it does not exist.</summary>
    public Task<RestaurantSnapshotDto?> FindByIdAsync(Guid restaurantId, CancellationToken ct);
}

public sealed record RestaurantSnapshotDto(Guid RestaurantId, bool IsApproved);

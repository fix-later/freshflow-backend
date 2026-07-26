namespace FreshFlow.Invoicing.Application.Abstractions;

/// <summary>
/// Cross-module read of a restaurant's invoicing/tax profile, plus userId → restaurantId resolution
/// for RBAC scoping. Implemented in Infrastructure via a keyless seam onto the Auth-owned
/// restaurants table.
/// </summary>
public interface IRestaurantReader
{
    public Task<RestaurantTaxProfile?> GetTaxProfileAsync(Guid restaurantId, CancellationToken ct);

    public Task<Guid?> FindRestaurantIdByUserIdAsync(Guid userId, CancellationToken ct);
}

public sealed record RestaurantTaxProfile(
    Guid RestaurantId,
    string Name,
    string? TaxCode,
    string? LegalName,
    string? Address,
    string? Email);

using FreshFlow.Orders.Application.Dtos;

namespace FreshFlow.Orders.Application.Abstractions;

/// <summary>
/// Cross-module read service enriching a restaurant's favorites with live product/market data.
/// Implemented in Infrastructure via a read-only projection — Orders has no project reference
/// to Catalog/Pricing.
/// </summary>
public interface IFavoriteReader
{
    public Task<IReadOnlyList<FavoriteItemDto>> ListAsync(Guid restaurantId, CancellationToken ct);
}

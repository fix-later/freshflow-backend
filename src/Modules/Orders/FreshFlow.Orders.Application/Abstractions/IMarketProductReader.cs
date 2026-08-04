namespace FreshFlow.Orders.Application.Abstractions;

/// <summary>
/// Cross-module read service for validating order items against live market product data
/// (UC-ORD-05). Implemented in Infrastructure via a read-only projection — Orders has no
/// project reference to Pricing/Catalog.
/// </summary>
public interface IMarketProductReader
{
    /// <summary>
    /// Returns the current price, name snapshot, and available quantity for a market product,
    /// or null if the market product (or its underlying product) does not exist / is deleted.
    /// </summary>
    public Task<MarketProductSnapshotDto?> FindAsync(Guid marketProductId, CancellationToken ct);
}

/// <summary>
/// Snapshot of a market product at the moment an order item references it — used to capture
/// <c>unit_price</c> and <c>product_name_snapshot</c> on the <c>OrderItem</c> at draft time.
/// </summary>
public sealed record MarketProductSnapshotDto(
    Guid MarketProductId,
    string ProductName,
    decimal CurrentPrice,
    int AvailableQuantity,
    int MinimumOrderQuantity = 1,
    string? VatRate = null,
    decimal? OriginLatitude = null,
    decimal? OriginLongitude = null);

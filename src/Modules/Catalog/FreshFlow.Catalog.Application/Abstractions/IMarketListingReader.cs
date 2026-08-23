namespace FreshFlow.Catalog.Application.Abstractions;

/// <summary>
/// Cross-module read of Pricing's <c>market_products</c> table, guarding product deactivation
/// (see docs/AUDIT-2026-08-23-business-flow-audit.md, finding C6).
/// </summary>
public interface IMarketListingReader
{
    public Task<int> CountActiveListingsAsync(Guid productId, CancellationToken ct);
}

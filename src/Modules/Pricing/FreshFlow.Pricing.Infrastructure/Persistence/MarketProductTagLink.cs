namespace FreshFlow.Pricing.Infrastructure.Persistence;

/// <summary>
/// Explicit join entity for the <c>MarketProduct.Tags</c> ↔ <c>Tag</c> skip-navigation
/// (table <c>market_product_tags</c>). Not a domain type — exists only so the join table can be
/// queried/bulk-deleted directly (e.g. <c>TagRepository.ClearAssignmentsAsync</c>) without a
/// second keyless-Row seam, since both sides already live in this module.
/// </summary>
internal sealed class MarketProductTagLink
{
    public Guid MarketProductId { get; set; }
    public Guid TagId { get; set; }
}

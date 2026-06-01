namespace FreshFlow.Auth.Infrastructure.CrossModule;

// Infrastructure-only EF entity — maps to the markets table that Pricing module owns.
// Auth reads this table to validate marketId when creating market_agent users.
internal sealed class MarketRow
{
    public Guid Id { get; set; }
    public bool IsActive { get; set; }
}

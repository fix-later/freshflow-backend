namespace FreshFlow.Pricing.Infrastructure.CrossModule;

/// <summary>
/// Infrastructure-only EF entity — read-only projection onto the user_market_assignments
/// table owned by the Auth module. Pricing reads UserId + MarketId to verify that a
/// market agent is assigned to the market they are trying to update prices for.
/// </summary>
internal sealed class UserMarketAssignmentRow
{
    public Guid UserId { get; set; }
    public Guid MarketId { get; set; }
}

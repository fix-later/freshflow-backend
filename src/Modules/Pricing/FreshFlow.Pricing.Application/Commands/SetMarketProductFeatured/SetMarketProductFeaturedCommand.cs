using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Pricing.Application.Commands.SetMarketProductFeatured;

/// <summary>
/// Marks (or unmarks) a market's product listing as a signature/featured item.
/// Featured items are pinned to the top of the market product board.
/// </summary>
public sealed record SetMarketProductFeaturedCommand(
    Guid MarketId, Guid ProductId, bool IsFeatured, Guid? Actor) : ICommand;

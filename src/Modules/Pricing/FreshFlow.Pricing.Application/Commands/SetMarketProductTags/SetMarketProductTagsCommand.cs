using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Pricing.Application.Commands.SetMarketProductTags;

/// <summary>
/// Replaces the tag set of a market's product listing. The special tag "nổi bật"
/// (<c>MarketProduct.FeaturedTag</c>) pins the listing to the top of the market product board.
/// </summary>
public sealed record SetMarketProductTagsCommand(
    Guid MarketId, Guid ProductId, IReadOnlyList<string> Tags, Guid? Actor) : ICommand;

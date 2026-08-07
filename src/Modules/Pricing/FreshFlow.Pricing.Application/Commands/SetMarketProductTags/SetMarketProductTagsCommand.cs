using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Pricing.Application.Commands.SetMarketProductTags;

/// <summary>
/// Replaces the tag assignment set of a market's product listing, by catalog <c>Tag.Id</c>.
/// A tag with <c>PinsToTop</c> pins the listing to the top of the market product board.
/// </summary>
public sealed record SetMarketProductTagsCommand(
    Guid MarketId, Guid ProductId, IReadOnlyList<Guid> TagIds, Guid? Actor) : ICommand;

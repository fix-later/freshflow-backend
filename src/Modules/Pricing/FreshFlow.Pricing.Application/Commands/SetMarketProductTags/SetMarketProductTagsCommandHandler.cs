using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Pricing.Application.Commands.SetMarketProductTags;

internal sealed class SetMarketProductTagsCommandHandler(
    IMarketProductRepository marketProducts, ITagRepository tags)
    : IRequestHandler<SetMarketProductTagsCommand, Result>
{
    public async Task<Result> Handle(SetMarketProductTagsCommand request, CancellationToken ct)
    {
        var marketProduct = await marketProducts.FindTrackedWithTagsByMarketAndProductAsync(
            request.MarketId, request.ProductId, ct);
        if (marketProduct is null)
            return Result.Failure(Error.NotFound("MARKET_PRODUCT", request.ProductId));

        var requestedIds = request.TagIds.Distinct().ToList();
        var found = await tags.FindByIdsAsync(requestedIds, ct);
        if (found.Count != requestedIds.Count)
        {
            var missing = requestedIds.Except(found.Select(t => t.Id)).First();
            return Result.Failure(Error.Validation(
                "VALIDATION_ERROR", $"Tag '{missing}' does not exist."));
        }

        marketProduct.SetTags(found, request.Actor);
        // marketProduct is already tracked (fetched via FindTrackedWithTagsByMarketAndProductAsync) —
        // no Track()/Update() reattach here, that would mark every related Tag Modified too.
        await marketProducts.SaveChangesAsync(ct);

        return Result.Success();
    }
}

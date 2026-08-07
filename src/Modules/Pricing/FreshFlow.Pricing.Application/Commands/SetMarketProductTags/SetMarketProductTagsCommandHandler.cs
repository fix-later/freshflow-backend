using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Pricing.Application.Commands.SetMarketProductTags;

internal sealed class SetMarketProductTagsCommandHandler(IMarketProductRepository marketProducts)
    : IRequestHandler<SetMarketProductTagsCommand, Result>
{
    public async Task<Result> Handle(SetMarketProductTagsCommand request, CancellationToken ct)
    {
        var marketProduct = await marketProducts.FindByMarketAndProductAsync(
            request.MarketId, request.ProductId, ct);
        if (marketProduct is null)
            return Result.Failure(Error.NotFound("MARKET_PRODUCT", request.ProductId));

        marketProduct.SetTags(request.Tags, request.Actor);
        marketProducts.Track(marketProduct);
        await marketProducts.SaveChangesAsync(ct);

        return Result.Success();
    }
}

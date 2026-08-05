using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Pricing.Application.Commands.SetMarketProductFeatured;

internal sealed class SetMarketProductFeaturedCommandHandler(IMarketProductRepository marketProducts)
    : IRequestHandler<SetMarketProductFeaturedCommand, Result>
{
    public async Task<Result> Handle(SetMarketProductFeaturedCommand request, CancellationToken ct)
    {
        var marketProduct = await marketProducts.FindByMarketAndProductAsync(
            request.MarketId, request.ProductId, ct);
        if (marketProduct is null)
            return Result.Failure(Error.NotFound("MARKET_PRODUCT", request.ProductId));

        marketProduct.SetFeatured(request.IsFeatured, request.Actor);
        marketProducts.Track(marketProduct);
        await marketProducts.SaveChangesAsync(ct);

        return Result.Success();
    }
}

using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Pricing.Application.Commands.DeleteMarketProduct;

internal sealed class DeleteMarketProductCommandHandler(IMarketProductRepository marketProducts)
    : IRequestHandler<DeleteMarketProductCommand, Result>
{
    public async Task<Result> Handle(DeleteMarketProductCommand request, CancellationToken ct)
    {
        var marketProduct = await marketProducts.FindByMarketAndProductAsync(
            request.MarketId, request.ProductId, ct);
        if (marketProduct is null)
            return Result.Failure(Error.NotFound("MARKET_PRODUCT", request.ProductId));

        if (marketProduct.ReservedQuantity > 0)
            return Result.Failure(Error.Conflict(
                "MARKET_PRODUCT_HAS_RESERVED_STOCK",
                $"{marketProduct.ReservedQuantity} unit(s) are reserved by confirmed orders. "
                + "Fulfil or cancel those orders before removing the listing."));

        marketProduct.Delete();
        marketProducts.Track(marketProduct);
        await marketProducts.SaveChangesAsync(ct);

        return Result.Success();
    }
}

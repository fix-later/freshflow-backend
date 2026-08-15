using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Pricing.Application.Queries.GetMarketProducts;

internal sealed class GetMarketProductsQueryHandler(
    IMarketProductReader reader,
    IPriceBoardReader priceBoardReader,
    ILogger<GetMarketProductsQueryHandler> logger)
    : IRequestHandler<GetMarketProductsQuery, Result<MarketProductPageDto>>
{
    public async Task<Result<MarketProductPageDto>> Handle(
        GetMarketProductsQuery request, CancellationToken ct)
    {
        if (!await reader.MarketExistsAsync(request.MarketId, ct))
            return Result<MarketProductPageDto>.Failure(
                Error.NotFound("MARKET", request.MarketId));

        var (items, nextCursor) = await reader.GetPageAsync(
            request.MarketId, request.Category, request.Cursor, request.PageSize, request.Tag, ct);

        // UC-PRI-09: overlay live price/quantity/availableQuantity from Redis.
        var liveItems = await OverlayLivePricesAsync(request.MarketId, items, ct);

        return Result<MarketProductPageDto>.Success(
            new MarketProductPageDto(liveItems, request.PageSize, nextCursor));
    }

    private async Task<IReadOnlyList<MarketProductItemDto>> OverlayLivePricesAsync(
        Guid marketId,
        IReadOnlyList<MarketProductItemDto> items,
        CancellationToken ct)
    {
        if (items.Count == 0) return items;

        IReadOnlyDictionary<Guid, LivePriceEntry> livePrices;
        try
        {
            var productIds = items.Select(i => i.ProductId).ToList();
            livePrices = await priceBoardReader.GetBatchAsync(marketId, productIds, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Price board read failed for MarketId={MarketId}; using DB prices",
                marketId);
            return items; // reader failure → keep items from GetPageAsync unchanged
        }

        if (livePrices.Count == 0) return items; // all miss → no-op

        return items
            .Select(i =>
            {
                if (!livePrices.TryGetValue(i.ProductId, out var live))
                    return i; // miss for this product → keep DB values
                return i with
                {
                    CurrentPrice = live.Price,
                    CurrentQuantity = live.Quantity,
                    AvailableQuantity = live.AvailableQuantity,
                };
            })
            .ToList()
            .AsReadOnly();
    }
}

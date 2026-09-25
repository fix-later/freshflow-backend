using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Caching;
using FreshFlow.Catalog.Application.Commands.Markets.Create;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using FreshFlow.SharedKernel.Caching;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;

namespace FreshFlow.Catalog.Application.Queries.Markets.GetMarkets;

internal sealed class GetMarketsQueryHandler(IMarketRepository markets, HybridCache cache)
    : IRequestHandler<GetMarketsQuery, Result<IReadOnlyList<MarketDto>>>
{
    public async Task<Result<IReadOnlyList<MarketDto>>> Handle(GetMarketsQuery request, CancellationToken ct)
    {
        var dtos = await cache.GetOrCreateAsync(
            RedisKeys.CatalogMarkets(request.ActiveOnly),
            async innerCt =>
            {
                var rows = await markets.GetAllAsync(request.ActiveOnly, innerCt);
                return (IReadOnlyList<MarketDto>)rows
                    .Select(CreateMarketCommandHandler.ToDto).ToList().AsReadOnly();
            },
            CatalogCache.ReferenceData,
            CatalogCache.Markets,
            ct);

        return Result<IReadOnlyList<MarketDto>>.Success(dtos);
    }
}

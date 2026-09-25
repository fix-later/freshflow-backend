using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Caching;
using FreshFlow.Catalog.Application.Commands.Markets.Create;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.Markets.Deactivate;

internal sealed class DeactivateMarketCommandHandler(IMarketRepository markets, CatalogCacheEvictor evictor)
    : IRequestHandler<DeactivateMarketCommand, Result<MarketDto>>
{
    public async Task<Result<MarketDto>> Handle(DeactivateMarketCommand request, CancellationToken ct)
    {
        var market = await markets.FindByIdAsync(request.Id, ct);
        if (market is null)
            return Result<MarketDto>.Failure(Error.NotFound("Market", request.Id));

        market.Deactivate();
        markets.Track(market);
        await markets.SaveChangesAsync(ct);

        await evictor.EvictAsync(CatalogCache.MarketsTag, ct);

        return Result<MarketDto>.Success(CreateMarketCommandHandler.ToDto(market));
    }
}

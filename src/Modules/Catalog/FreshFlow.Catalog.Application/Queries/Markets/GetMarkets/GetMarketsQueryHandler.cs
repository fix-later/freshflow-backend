using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Commands.Markets.Create;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Queries.Markets.GetMarkets;

internal sealed class GetMarketsQueryHandler(IMarketRepository markets)
    : IRequestHandler<GetMarketsQuery, Result<IReadOnlyList<MarketDto>>>
{
    public async Task<Result<IReadOnlyList<MarketDto>>> Handle(GetMarketsQuery request, CancellationToken ct)
    {
        var rows = await markets.GetAllAsync(request.ActiveOnly, ct);
        var dtos = rows.Select(CreateMarketCommandHandler.ToDto).ToList().AsReadOnly();
        return Result<IReadOnlyList<MarketDto>>.Success(dtos);
    }
}

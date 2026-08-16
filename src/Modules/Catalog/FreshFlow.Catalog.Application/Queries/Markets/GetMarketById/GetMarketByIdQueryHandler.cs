using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Commands.Markets.Create;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Queries.Markets.GetMarketById;

internal sealed class GetMarketByIdQueryHandler(IMarketRepository markets)
    : IRequestHandler<GetMarketByIdQuery, Result<MarketDto>>
{
    public async Task<Result<MarketDto>> Handle(GetMarketByIdQuery request, CancellationToken ct)
    {
        var market = await markets.FindByIdAsync(request.Id, ct);
        if (market is null)
            return Result<MarketDto>.Failure(Error.NotFound("Market", request.Id));

        return Result<MarketDto>.Success(CreateMarketCommandHandler.ToDto(market));
    }
}

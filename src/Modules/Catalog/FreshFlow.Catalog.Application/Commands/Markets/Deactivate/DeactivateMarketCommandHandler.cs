using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Commands.Markets.Create;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.Markets.Deactivate;

internal sealed class DeactivateMarketCommandHandler(IMarketRepository markets)
    : IRequestHandler<DeactivateMarketCommand, Result<MarketDto>>
{
    public async Task<Result<MarketDto>> Handle(DeactivateMarketCommand request, CancellationToken ct)
    {
        var market = await markets.FindByIdAsync(request.Id, ct);
        if (market is null)
            return Result<MarketDto>.Failure(Error.NotFound("Market", request.Id));

        market.Deactivate();
        await markets.SaveChangesAsync(ct);

        return Result<MarketDto>.Success(CreateMarketCommandHandler.ToDto(market));
    }
}

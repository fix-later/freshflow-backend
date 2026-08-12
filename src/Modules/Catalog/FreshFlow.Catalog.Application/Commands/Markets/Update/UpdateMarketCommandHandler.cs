using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Commands.Markets.Create;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.Markets.Update;

internal sealed class UpdateMarketCommandHandler(IMarketRepository markets)
    : IRequestHandler<UpdateMarketCommand, Result<MarketDto>>
{
    public async Task<Result<MarketDto>> Handle(UpdateMarketCommand request, CancellationToken ct)
    {
        var market = await markets.FindByIdAsync(request.Id, ct);
        if (market is null)
            return Result<MarketDto>.Failure(Error.NotFound("Market", request.Id));

        market.Update(
            request.Name,
            request.Location,
            request.Address,
            request.Latitude,
            request.Longitude,
            request.ImageUrl,
            request.Description,
            request.Code);
        markets.Track(market);
        await markets.SaveChangesAsync(ct);

        return Result<MarketDto>.Success(CreateMarketCommandHandler.ToDto(market));
    }
}

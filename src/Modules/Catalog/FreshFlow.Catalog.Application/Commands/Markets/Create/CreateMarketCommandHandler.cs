using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.Catalog.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.Markets.Create;

internal sealed class CreateMarketCommandHandler(IMarketRepository markets)
    : IRequestHandler<CreateMarketCommand, Result<MarketDto>>
{
    public async Task<Result<MarketDto>> Handle(CreateMarketCommand request, CancellationToken ct)
    {
        var market = new Market(
            request.Name,
            request.Location,
            request.Address,
            request.Latitude,
            request.Longitude);

        await markets.AddAsync(market, ct);
        await markets.SaveChangesAsync(ct);

        return Result<MarketDto>.Success(ToDto(market));
    }

    internal static MarketDto ToDto(Market m) =>
        new(m.Id, m.Name, m.Location, m.Address, m.Latitude, m.Longitude,
            m.IsActive, m.CreatedAt, m.UpdatedAt);
}

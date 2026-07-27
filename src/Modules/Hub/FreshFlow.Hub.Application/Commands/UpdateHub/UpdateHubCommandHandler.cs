using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Hub.Application.Commands.UpdateHub;

internal sealed class UpdateHubCommandHandler(IHubRepository hubs, IMarketReader markets)
    : IRequestHandler<UpdateHubCommand, Result<HubDto>>
{
    public async Task<Result<HubDto>> Handle(UpdateHubCommand request, CancellationToken ct)
    {
        var hub = await hubs.FindByIdAsync(request.HubId, ct);
        if (hub is null)
            return Result<HubDto>.Failure(Error.NotFound("HUB", request.HubId));

        if (request.CapacityKg < hub.OccupiedCapacityKg)
        {
            return Result<HubDto>.Failure(
                Error.Validation(
                    "HUB_CAPACITY_BELOW_OCCUPIED",
                    "Hub capacity cannot be less than occupied capacity."));
        }

        if (request.MarketId.HasValue && request.MarketId != hub.MarketId)
        {
            var market = await markets.FindAsync(request.MarketId.Value, ct);
            if (market is null)
                return Result<HubDto>.Failure(Error.NotFound("MARKET", request.MarketId.Value));

            if (!market.IsActive)
                return Result<HubDto>.Failure(Error.Validation(
                    "MARKET_INACTIVE", $"Market '{request.MarketId}' is inactive."));

            if (await hubs.HasActiveForMarketAsync(request.MarketId.Value, ct))
                return Result<HubDto>.Failure(Error.Conflict(
                    "HUB_ALREADY_CONFIGURED_FOR_MARKET",
                    $"Market '{request.MarketId}' already has an active hub."));

            hub.AssignMarket(request.MarketId.Value);
        }

        hub.Update(
            request.Name,
            request.Address,
            request.Latitude,
            request.Longitude,
            request.CapacityKg,
            request.ManagedBy);

        try
        {
            await hubs.SaveChangesAsync(ct);
        }
        catch (HubMarketConflictException)
        {
            return Result<HubDto>.Failure(Error.Conflict(
                "HUB_ALREADY_CONFIGURED_FOR_MARKET",
                $"Market '{request.MarketId}' already has an active hub."));
        }
        catch (HubConcurrencyException)
        {
            return Result<HubDto>.Failure(
                Error.Conflict(
                    "OPTIMISTIC_CONCURRENCY_CONFLICT",
                    "Hub was updated by another request. Please refresh and retry."));
        }

        return Result<HubDto>.Success(hub.ToDto());
    }
}

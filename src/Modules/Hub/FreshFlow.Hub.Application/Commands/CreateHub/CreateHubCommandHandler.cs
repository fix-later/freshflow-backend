using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.Application.Commands.CreateHub;

internal sealed class CreateHubCommandHandler(IHubRepository hubs, IMarketReader markets)
    : IRequestHandler<CreateHubCommand, Result<HubDto>>
{
    public async Task<Result<HubDto>> Handle(CreateHubCommand request, CancellationToken ct)
    {
        var market = await markets.FindAsync(request.MarketId, ct);
        if (market is null)
            return Result<HubDto>.Failure(Error.NotFound("MARKET", request.MarketId));

        if (!market.IsActive)
        {
            return Result<HubDto>.Failure(Error.Validation(
                "MARKET_INACTIVE",
                $"Market '{request.MarketId}' is inactive."));
        }

        if (await hubs.HasActiveForMarketAsync(request.MarketId, ct))
            return MarketAlreadyHasHub(request.MarketId);

        var hub = HubEntity.Create(
            request.Name,
            request.Address,
            request.Latitude,
            request.Longitude,
            request.CapacityKg,
            request.ManagedBy,
            request.MarketId);

        await hubs.AddAsync(hub, ct);
        try
        {
            await hubs.SaveChangesAsync(ct);
        }
        catch (HubMarketConflictException)
        {
            return MarketAlreadyHasHub(request.MarketId);
        }
        catch (HubConcurrencyException)
        {
            return Result<HubDto>.Failure(
                Error.Conflict(
                    "OPTIMISTIC_CONCURRENCY_CONFLICT",
                    "Hub save conflicted with another request. Please refresh and retry."));
        }

        return Result<HubDto>.Success(hub.ToDto());
    }

    private static Result<HubDto> MarketAlreadyHasHub(Guid marketId) =>
        Result<HubDto>.Failure(Error.Conflict(
            "HUB_ALREADY_CONFIGURED_FOR_MARKET",
            $"Market '{marketId}' already has an active hub."));
}

using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Application.Mappings;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Hub.Application.Commands.CreateCrossDock;

internal sealed class CreateCrossDockCommandHandler(
    IHubRepository hubs,
    IHubInboundRepository inbounds,
    ICrossDockRepository transfers,
    IDeliveryRouteReader routes)
    : IRequestHandler<CreateCrossDockCommand, Result<CrossDockTransferDto>>
{
    public async Task<Result<CrossDockTransferDto>> Handle(CreateCrossDockCommand request, CancellationToken ct)
    {
        var hub = await hubs.FindByIdAsync(request.HubId, ct);
        if (hub is null)
            return Result<CrossDockTransferDto>.Failure(Error.NotFound("HUB", request.HubId));

        var inbound = await inbounds.FindByIdForHubAsync(request.HubId, request.InboundEventId, ct);
        if (inbound is null)
            return Result<CrossDockTransferDto>.Failure(Error.NotFound("HUB_INBOUND_EVENT", request.InboundEventId));

        if (inbound.Status != HubInboundEvent.StatusArrivedAtHub)
        {
            return Result<CrossDockTransferDto>.Failure(
                Error.Validation("INBOUND_NOT_ARRIVED", "Inbound event must be arrived at hub before cross-docking."));
        }

        var route = await routes.FindByIdAsync(request.OutboundRouteId, ct);
        if (route is null)
        {
            return Result<CrossDockTransferDto>.Failure(
                Error.Validation("OUTBOUND_ROUTE_INVALID", "Outbound route does not exist."));
        }

        var transfer = CrossDockTransfer.Create(
            request.HubId,
            request.InboundEventId,
            request.OutboundRouteId,
            request.Notes);

        await transfers.AddAsync(transfer, ct);
        await transfers.SaveChangesAsync(ct);

        return Result<CrossDockTransferDto>.Success(transfer.ToDto());
    }
}

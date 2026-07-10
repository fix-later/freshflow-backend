using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Application.Mappings;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Hub.Application.Commands.CreateHandover;

internal sealed class CreateHandoverCommandHandler(
    IHubRepository hubs,
    IDeliveryRouteReader routes,
    IHubOutboundRepository outbounds,
    IHubHandoverRepository handovers)
    : IRequestHandler<CreateHandoverCommand, Result<HubHandoverDto>>
{
    public async Task<Result<HubHandoverDto>> Handle(CreateHandoverCommand request, CancellationToken ct)
    {
        var hub = await hubs.FindByIdAsync(request.HubId, ct);
        if (hub is null)
            return Result<HubHandoverDto>.Failure(Error.NotFound("HUB", request.HubId));

        var route = await routes.FindByIdAsync(request.DeliveryRouteId, ct);
        if (route is null)
            return Result<HubHandoverDto>.Failure(Error.NotFound("DELIVERY_ROUTE", request.DeliveryRouteId));

        if (route.DriverUserId is null)
        {
            return Result<HubHandoverDto>.Failure(
                Error.Validation("ROUTE_HAS_NO_DRIVER", "Delivery route has no assigned driver."));
        }

        if (request.DriverUserId != route.DriverUserId.Value)
        {
            return Result<HubHandoverDto>.Failure(
                Error.Validation("DRIVER_ROUTE_MISMATCH", "Driver does not match the delivery route assignment."));
        }

        if (request.OutboundEventId.HasValue &&
            await outbounds.FindByIdForHubAsync(request.HubId, request.OutboundEventId.Value, ct) is null)
        {
            return Result<HubHandoverDto>.Failure(
                Error.NotFound("HUB_OUTBOUND_EVENT", request.OutboundEventId.Value));
        }

        var handover = HubHandoverEvent.Create(
            request.HubId,
            request.DeliveryRouteId,
            request.DriverUserId,
            request.OutboundEventId,
            request.HandedOverBy,
            request.Notes);

        await handovers.AddAsync(handover, ct);
        await handovers.SaveChangesAsync(ct);

        return Result<HubHandoverDto>.Success(handover.ToDto());
    }
}

using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Application.Mappings;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Hub.Application.Commands.RecordInbound;

internal sealed class RecordInboundCommandHandler(
    IHubRepository hubs,
    IHubInboundRepository inbounds)
    : IRequestHandler<RecordInboundCommand, Result<HubInboundDto>>
{
    public async Task<Result<HubInboundDto>> Handle(RecordInboundCommand request, CancellationToken ct)
    {
        var hub = await hubs.FindByIdAsync(request.HubId, ct);
        if (hub is null)
            return Result<HubInboundDto>.Failure(Error.NotFound("HUB", request.HubId));

        if (request.DeliveryScheduleId.HasValue &&
            await inbounds.DeliveryScheduleExistsAsync(request.HubId, request.DeliveryScheduleId.Value, ct))
        {
            return Result<HubInboundDto>.Failure(
                Error.Conflict("ALREADY_RECEIVED", "Inbound delivery schedule has already been recorded."));
        }

        var inbound = HubInboundEvent.Record(
            request.HubId,
            request.SourceMarketId,
            deliveryRouteId: null,
            request.DeliveryScheduleId,
            request.Items.Select(item => item.ToDomain()).ToList().AsReadOnly(),
            request.ArrivedAt);

        await inbounds.AddAsync(inbound, ct);
        await inbounds.SaveChangesAsync(ct);

        return Result<HubInboundDto>.Success(inbound.ToDto());
    }
}

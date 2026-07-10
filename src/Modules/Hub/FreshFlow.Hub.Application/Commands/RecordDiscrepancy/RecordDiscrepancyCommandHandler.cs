using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Application.Mappings;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Hub.Application.Commands.RecordDiscrepancy;

internal sealed class RecordDiscrepancyCommandHandler(
    IHubRepository hubs,
    IHubInboundRepository inbounds,
    IHubDiscrepancyRepository discrepancies,
    IOrderLookupReader orders)
    : IRequestHandler<RecordDiscrepancyCommand, Result<HubDiscrepancyDto>>
{
    public async Task<Result<HubDiscrepancyDto>> Handle(RecordDiscrepancyCommand request, CancellationToken ct)
    {
        var hub = await hubs.FindByIdAsync(request.HubId, ct);
        if (hub is null)
            return Result<HubDiscrepancyDto>.Failure(Error.NotFound("HUB", request.HubId));

        if (!await inbounds.ExistsForHubAsync(request.HubId, request.InboundEventId, ct))
            return Result<HubDiscrepancyDto>.Failure(Error.NotFound("HUB_INBOUND_EVENT", request.InboundEventId));

        var order = await orders.FindByOrderItemIdAsync(request.OrderItemId, ct);
        if (order is null)
            return Result<HubDiscrepancyDto>.Failure(Error.NotFound("ORDER_ITEM", request.OrderItemId));

        var discrepancy = HubDiscrepancy.Create(
            request.HubId,
            request.InboundEventId,
            order.OrderId,
            request.OrderItemId,
            request.AffectedQuantity,
            request.ConditionStatus,
            request.Notes);

        await discrepancies.AddAsync(discrepancy, ct);
        await discrepancies.SaveChangesAsync(ct);

        return Result<HubDiscrepancyDto>.Success(discrepancy.ToDto());
    }
}

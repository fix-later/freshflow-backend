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

        var inbound = await inbounds.FindByIdForHubAsync(request.HubId, request.InboundEventId, ct);
        if (inbound is null)
            return Result<HubDiscrepancyDto>.Failure(Error.NotFound("HUB_INBOUND_EVENT", request.InboundEventId));

        if (inbound.Status != HubInboundEvent.StatusArrivedAtHub)
        {
            return Result<HubDiscrepancyDto>.Failure(
                Error.Validation("INBOUND_NOT_ARRIVED", "Inbound event must be arrived at hub before recording discrepancies."));
        }

        var order = await orders.FindByOrderItemIdAsync(request.OrderItemId, ct);
        if (order is null)
            return Result<HubDiscrepancyDto>.Failure(Error.NotFound("ORDER_ITEM", request.OrderItemId));

        if (!inbound.Items.Any(item => item.MarketProductId == order.MarketProductId))
        {
            return Result<HubDiscrepancyDto>.Failure(
                Error.Validation("ORDER_ITEM_NOT_IN_INBOUND", "Order item does not match any product in this inbound event."));
        }

        var maxAffectedQuantity = order.ActualQuantity ?? order.Quantity;
        if (request.AffectedQuantity > maxAffectedQuantity)
        {
            return Result<HubDiscrepancyDto>.Failure(
                Error.Validation("INVALID_ISSUE_QUANTITY", "AffectedQuantity cannot exceed the order item quantity."));
        }

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

using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Application.Mappings;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Hub.Application.Commands.RecordOutbound;

internal sealed class RecordOutboundCommandHandler(
    IHubRepository hubs,
    IHubInventoryRepository inventory,
    IHubOutboundRepository outbounds,
    IDeliveryRouteReader routes)
    : IRequestHandler<RecordOutboundCommand, Result<HubOutboundEventDto>>
{
    public async Task<Result<HubOutboundEventDto>> Handle(RecordOutboundCommand request, CancellationToken ct)
    {
        var hub = await hubs.FindByIdAsync(request.HubId, ct);
        if (hub is null)
            return Result<HubOutboundEventDto>.Failure(Error.NotFound("HUB", request.HubId));

        var route = await routes.FindByIdAsync(request.DestinationRouteId, ct);
        if (route is null)
        {
            return Result<HubOutboundEventDto>.Failure(
                Error.Validation("OUTBOUND_ROUTE_INVALID", "Destination route does not exist."));
        }

        var items = request.Items.Select(item => item.ToDomain()).ToList().AsReadOnly();
        var totalQuantityKg = items.Sum(item => item.QuantityKg);
        if (hub.OccupiedCapacityKg < totalQuantityKg)
            return InsufficientStock();

        var inventoryRows = new List<(HubInventory Inventory, decimal Quantity)>();
        foreach (var group in items.GroupBy(item => item.MarketProductId))
        {
            var quantity = group.Sum(item => item.QuantityKg);
            var current = await inventory.FindByHubAndMarketProductAsync(request.HubId, group.Key, ct);
            if (current is null || current.QuantityOut + quantity > current.QuantityIn)
                return InsufficientStock();

            inventoryRows.Add((current, quantity));
        }

        var outbound = HubOutboundEvent.Record(
            request.HubId,
            request.DestinationRouteId,
            items,
            request.DispatchedAt);

        foreach (var row in inventoryRows)
            row.Inventory.AddOutbound(row.Quantity);

        hub.ApplyOutbound(totalQuantityKg);
        await outbounds.AddAsync(outbound, ct);
        await outbounds.SaveChangesAsync(ct);

        return Result<HubOutboundEventDto>.Success(outbound.ToDto());
    }

    private static Result<HubOutboundEventDto> InsufficientStock() =>
        Result<HubOutboundEventDto>.Failure(
            Error.Validation("INSUFFICIENT_HUB_STOCK", "Hub stock is not sufficient for this outbound dispatch."));
}

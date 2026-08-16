using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Application.Mappings;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Hub.Application.Commands.MarkLineSorted;

internal sealed class MarkLineSortedCommandHandler(
    IHubRepository hubs,
    IHubSortingProgressRepository progress,
    IOrderLookupReader orders,
    IHubRestaurantOrderReader hubOrders)
    : IRequestHandler<MarkLineSortedCommand, Result<HubSortingProgressDto>>
{
    public async Task<Result<HubSortingProgressDto>> Handle(MarkLineSortedCommand request, CancellationToken ct)
    {
        var hub = await hubs.FindByIdAsync(request.HubId, ct);
        if (hub is null)
            return Result<HubSortingProgressDto>.Failure(Error.NotFound("HUB", request.HubId));

        var order = await orders.FindByOrderItemIdAsync(request.OrderItemId, ct);
        if (order is null)
            return Result<HubSortingProgressDto>.Failure(Error.NotFound("ORDER_ITEM", request.OrderItemId));

        var belongsToHub = (await hubOrders.ListByHubAndServiceDateAsync(
                request.HubId, ["AtHub"], request.ServiceDate, ct))
            .Any(item => item.OrderId == order.OrderId);
        if (!belongsToHub)
            return Result<HubSortingProgressDto>.Failure(Error.Validation(
                "ORDER_NOT_AT_HUB", "Order item does not belong to this Hub and service date."));

        var requiredQuantityKg = order.ActualQuantity ?? order.Quantity;
        if (requiredQuantityKg <= 0m || request.SortedQuantityKg > requiredQuantityKg)
        {
            return Result<HubSortingProgressDto>.Failure(Error.Validation(
                "INVALID_SORTED_QUANTITY",
                $"SortedQuantityKg must be greater than zero and at most {requiredQuantityKg}."));
        }


        var line = await progress.FindByHubDateAndOrderItemAsync(
            request.HubId, request.ServiceDate, request.OrderItemId, ct);
        if (line is null)
        {
            line = HubSortingProgress.Create(request.HubId, request.ServiceDate, request.OrderItemId);
            await progress.AddAsync(line, ct);
        }

        if (request.SortedQuantityKg < line.SortedQuantityKg)
        {
            return Result<HubSortingProgressDto>.Failure(Error.Validation(
                "INVALID_SORTED_QUANTITY",
                $"SortedQuantityKg must be between {line.SortedQuantityKg} and {requiredQuantityKg}."));
        }

        line.UpdateSortedQuantity(
            request.SortedQuantityKg, requiredQuantityKg, request.ActorUserId, DateTime.UtcNow);

        await progress.SaveChangesAsync(ct);

        return Result<HubSortingProgressDto>.Success(line.ToDto(order));
    }
}

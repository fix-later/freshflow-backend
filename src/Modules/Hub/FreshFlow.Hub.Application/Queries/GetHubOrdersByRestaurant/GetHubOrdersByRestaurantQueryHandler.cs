using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Hub.Application.Queries.GetHubOrdersByRestaurant;

internal sealed class GetHubOrdersByRestaurantQueryHandler(
    IHubRestaurantOrderReader orders,
    IHubOrderLineReader lines,
    IHubSortingProgressRepository progress)
    : IRequestHandler<GetHubOrdersByRestaurantQuery, Result<HubOrdersByRestaurantDto>>
{
    public async Task<Result<HubOrdersByRestaurantDto>> Handle(
        GetHubOrdersByRestaurantQuery request,
        CancellationToken ct)
    {
        IReadOnlyCollection<string> statuses = request.IncludeBatched
            ? ["AtHub", "Batched"]
            : ["AtHub"];
        var hubOrders = await orders.ListByHubAndServiceDateAsync(
            request.HubId, statuses, request.ServiceDate, ct);
        var linesByOrder = (await lines.GetLinesByOrdersAsync(
                hubOrders.Select(order => order.OrderId).ToList(), ct))
            .ToLookup(line => line.OrderId);
        var progressByItem = (await progress.ListByHubAndDateAsync(
                request.HubId, request.ServiceDate, ct))
            .ToDictionary(item => item.OrderItemId);

        var restaurants = hubOrders
            .GroupBy(order => new { order.RestaurantId, order.RestaurantName })
            .Select(group => ToRestaurantDto(
                group.Key.RestaurantId,
                group.Key.RestaurantName,
                group.Select(order => order.OrderId),
                linesByOrder,
                progressByItem))
            .ToList();

        return Result<HubOrdersByRestaurantDto>.Success(
            new HubOrdersByRestaurantDto(request.HubId, request.ServiceDate, restaurants));
    }


    private static HubRestaurantOrdersDto ToRestaurantDto(
        Guid restaurantId,
        string restaurantName,
        IEnumerable<Guid> orderIds,
        ILookup<Guid, HubOrderLineDto> linesByOrder,
        IReadOnlyDictionary<Guid, HubSortingProgress> progressByItem)
    {
        var orders = orderIds.Distinct().Select(orderId =>
        {
            var items = linesByOrder[orderId]
                .Select(line => ToSortingItem(
                    line, progressByItem.GetValueOrDefault(line.OrderItemId)))
                .ToList();
            var status = items.Count > 0 &&
                         items.All(item => item.Status == HubSortingProgress.StatusSorted)
                ? HubSortingProgress.StatusSorted
                : HubSortingProgress.StatusPending;
            return new HubSortingOrderDto(orderId, status, items);
        }).ToList();

        return new HubRestaurantOrdersDto(
            restaurantId, restaurantName, orders.Count, orders);
    }

    private static HubOrderSortingItemDto ToSortingItem(
        HubOrderLineDto line,
        HubSortingProgress? progress)
    {
        var required = line.ActualQuantity ?? line.OrderedQuantity;
        var sorted = progress?.SortedQuantityKg ?? 0m;
        var remaining = Math.Max(required - sorted, 0m);
        var packageCount = line.CapacityKg is > 0m
            ? checked((int)Math.Ceiling(required / line.CapacityKg.Value))
            : (int?)null;

        return new HubOrderSortingItemDto(
            line.OrderItemId,
            line.ProductName,
            line.MarketProductId,
            line.ProductId,
            line.Unit,
            line.OrderedQuantity,
            required,
            line.PackingCode,
            line.CapacityKg,
            packageCount,
            sorted,
            remaining,
            remaining == 0m ? HubSortingProgress.StatusSorted : HubSortingProgress.StatusPending);
    }
}

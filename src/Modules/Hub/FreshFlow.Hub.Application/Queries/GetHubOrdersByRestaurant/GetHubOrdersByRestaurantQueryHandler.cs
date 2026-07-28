using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Hub.Application.Queries.GetHubOrdersByRestaurant;

internal sealed class GetHubOrdersByRestaurantQueryHandler(
    IHubRestaurantOrderReader orders,
    IHubOrderLineReader lines)
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

        var restaurants = hubOrders
            .GroupBy(order => new { order.RestaurantId, order.RestaurantName })
            .Select(group => new HubRestaurantOrdersDto(
                group.Key.RestaurantId,
                group.Key.RestaurantName,
                group.Select(order => order.OrderId).Distinct().Count(),
                group.SelectMany(order => linesByOrder[order.OrderId]).ToList()))
            .ToList();

        return Result<HubOrdersByRestaurantDto>.Success(
            new HubOrdersByRestaurantDto(request.HubId, request.ServiceDate, restaurants));
    }
}

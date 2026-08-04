using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Queries.GetDriverRoutesToday;

internal sealed class GetDriverRoutesTodayQueryHandler(
    IDeliveryRouteRepository routes,
    IDeliveryRepository deliveries,
    TimeProvider timeProvider)
    : IRequestHandler<GetDriverRoutesTodayQuery, Result<IReadOnlyList<DriverRouteDto>>>
{
    private static readonly RouteStatus[] VisibleStatuses = [RouteStatus.assigned, RouteStatus.in_progress];
    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

    public async Task<Result<IReadOnlyList<DriverRouteDto>>> Handle(
        GetDriverRoutesTodayQuery request,
        CancellationToken ct)
    {
        var serviceDate = request.ServiceDate ?? DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), VietnamTimeZone).DateTime);

        var candidateRoutes = await routes.GetByDriverAndDateAsync(request.DriverUserId, serviceDate, ct);
        var visibleRoutes = candidateRoutes
            .Where(route => VisibleStatuses.Contains(route.Status))
            .ToList();

        if (visibleRoutes.Count == 0)
            return Result<IReadOnlyList<DriverRouteDto>>.Success([]);

        var routeIds = visibleRoutes.Select(route => route.Id).ToArray();
        var routeDeliveries = await deliveries.GetByRouteIdsAsync(routeIds, ct);
        var deliveriesByRoute = routeDeliveries
            .GroupBy(delivery => delivery.DeliveryRouteId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var dtos = visibleRoutes
            .Select(route => MapRoute(route, deliveriesByRoute))
            .ToList()
            .AsReadOnly();

        return Result<IReadOnlyList<DriverRouteDto>>.Success(dtos);
    }

    private static DriverRouteDto MapRoute(
        DeliveryRoute route,
        IReadOnlyDictionary<Guid, List<Delivery>> deliveriesByRoute)
    {
        IReadOnlyList<DriverDeliveryDto> deliveryDtos = deliveriesByRoute.TryGetValue(route.Id, out var items)
            ? items
                .OrderBy(delivery => delivery.SequenceNumber)
                .ThenBy(delivery => delivery.Id)
                .Select(delivery => new DriverDeliveryDto(
                    delivery.Id,
                    delivery.OrderId,
                    delivery.SequenceNumber,
                    delivery.Status,
                    delivery.EstimatedArrival,
                    delivery.ActualArrival))
                .ToList()
                .AsReadOnly()
            : [];

        var stops = route.Stops
            .Select(stop => new RouteStopDto(
                stop.StopOrder,
                stop.EntityType.ToString(),
                stop.EntityId,
                stop.EntityName,
                stop.Latitude,
                stop.Longitude,
                stop.EstimatedArrivalAt,
                stop.EstimatedDepartureAt))
            .ToList()
            .AsReadOnly();

        return new DriverRouteDto(
            route.Id,
            route.ServiceDate,
            route.Status.ToString(),
            stops,
            deliveryDtos);
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }
}

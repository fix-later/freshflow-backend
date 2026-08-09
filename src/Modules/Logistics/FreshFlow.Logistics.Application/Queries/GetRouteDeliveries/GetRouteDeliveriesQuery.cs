using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Logistics.Application.Queries.GetRouteDeliveries;

public sealed record GetRouteDeliveriesQuery(Guid RouteId)
    : IQuery<IReadOnlyList<DriverDeliveryDto>>;

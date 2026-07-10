using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Logistics.Application.Queries.DeliveryZones.GetDeliveryZones;

public sealed record GetDeliveryZonesQuery(
    bool ActiveOnly = true) : IQuery<IReadOnlyList<DeliveryZoneDto>>;

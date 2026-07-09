using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Logistics.Application.Queries.DeliveryZones.GetDeliveryZoneById;

public sealed record GetDeliveryZoneByIdQuery(Guid Id) : IQuery<DeliveryZoneDto>;

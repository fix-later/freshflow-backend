using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Queries.EstimateOrderShipment;

public sealed record EstimateOrderShipmentQuery(Guid OrderId, Guid? VehicleId = null)
    : IRequest<Result<ShipmentEstimateDto>>;

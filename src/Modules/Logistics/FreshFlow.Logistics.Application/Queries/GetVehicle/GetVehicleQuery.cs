using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Queries.GetVehicle;

public sealed record GetVehicleQuery(Guid Id) : IRequest<Result<VehicleDto>>;

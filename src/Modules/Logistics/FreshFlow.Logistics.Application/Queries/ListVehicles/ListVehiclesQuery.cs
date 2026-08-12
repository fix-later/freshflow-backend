using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Queries.ListVehicles;

public sealed record ListVehiclesQuery(
    string? Cursor = null,
    int PageSize = 50,
    bool? IsActive = null,
    Guid? HubId = null) : IRequest<Result<VehiclePageDto>>;

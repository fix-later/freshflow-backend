using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Queries.ListVehicles;

internal sealed class ListVehiclesQueryHandler(IVehicleRepository vehicles)
    : IRequestHandler<ListVehiclesQuery, Result<VehiclePageDto>>
{
    public async Task<Result<VehiclePageDto>> Handle(ListVehiclesQuery request, CancellationToken ct)
    {
        var (items, nextCursor) = await vehicles.GetPageAsync(
            request.Cursor,
            request.PageSize,
            request.IsActive,
            ct);

        var dtos = items.Select(vehicle => vehicle.ToDto()).ToList().AsReadOnly();
        return Result<VehiclePageDto>.Success(
            new VehiclePageDto(dtos, request.PageSize, nextCursor));
    }
}

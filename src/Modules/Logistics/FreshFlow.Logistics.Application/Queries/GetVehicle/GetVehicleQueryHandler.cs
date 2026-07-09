using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Queries.GetVehicle;

internal sealed class GetVehicleQueryHandler(IVehicleRepository vehicles)
    : IRequestHandler<GetVehicleQuery, Result<VehicleDto>>
{
    public async Task<Result<VehicleDto>> Handle(GetVehicleQuery request, CancellationToken ct)
    {
        var vehicle = await vehicles.FindByIdAsync(request.Id, ct);
        return vehicle is null
            ? Result<VehicleDto>.Failure(Error.NotFound("VEHICLE", request.Id))
            : Result<VehicleDto>.Success(vehicle.ToDto());
    }
}

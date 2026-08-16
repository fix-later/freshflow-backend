using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.Catalog.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.Units.Update;

internal sealed class UpdateUnitCommandHandler(IUnitOfMeasurementRepository units)
    : IRequestHandler<UpdateUnitCommand, Result<UnitDto>>
{
    public async Task<Result<UnitDto>> Handle(UpdateUnitCommand request, CancellationToken ct)
    {
        var unit = await units.FindByIdAsync(request.Id, ct);
        if (unit is null)
            return Result<UnitDto>.Failure(Error.NotFound("UnitOfMeasurement", request.Id));

        // Skip duplicate-name check if the name is unchanged
        if (!string.Equals(unit.Name, request.Name, StringComparison.OrdinalIgnoreCase)
            && await units.ExistsByNameAsync(request.Name, ct))
            return Result<UnitDto>.Failure(
                Error.Conflict("UNIT_NAME_CONFLICT", $"A unit named '{request.Name}' already exists."));

        unit.Update(request.Name, request.Abbreviation);
        await units.SaveChangesAsync(ct);

        return Result<UnitDto>.Success(unit.ToDto());
    }
}

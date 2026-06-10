using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.Catalog.Application.Mappings;
using FreshFlow.Catalog.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.Units.Create;

internal sealed class CreateUnitCommandHandler(IUnitOfMeasurementRepository units)
    : IRequestHandler<CreateUnitCommand, Result<UnitDto>>
{
    public async Task<Result<UnitDto>> Handle(CreateUnitCommand request, CancellationToken ct)
    {
        if (await units.ExistsByNameAsync(request.Name, ct))
            return Result<UnitDto>.Failure(
                Error.Conflict("UNIT_NAME_CONFLICT", $"An active unit named '{request.Name}' already exists."));

        var unit = new UnitOfMeasurement(request.Name, request.Abbreviation);
        await units.AddAsync(unit, ct);
        await units.SaveChangesAsync(ct);

        return Result<UnitDto>.Success(unit.ToDto());
    }
}

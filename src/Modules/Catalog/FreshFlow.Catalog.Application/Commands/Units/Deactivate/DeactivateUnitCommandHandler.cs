using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.Catalog.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.Units.Deactivate;

internal sealed class DeactivateUnitCommandHandler(IUnitOfMeasurementRepository units)
    : IRequestHandler<DeactivateUnitCommand, Result<UnitDto>>
{
    public async Task<Result<UnitDto>> Handle(DeactivateUnitCommand request, CancellationToken ct)
    {
        var unit = await units.FindByIdAsync(request.Id, ct);
        if (unit is null)
            return Result<UnitDto>.Failure(Error.NotFound("UnitOfMeasurement", request.Id));

        unit.Deactivate();
        await units.SaveChangesAsync(ct);

        return Result<UnitDto>.Success(unit.ToDto());
    }
}

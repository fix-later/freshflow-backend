using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.Catalog.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Queries.Units.GetUnitById;

internal sealed class GetUnitByIdQueryHandler(IUnitOfMeasurementRepository units)
    : IRequestHandler<GetUnitByIdQuery, Result<UnitDto>>
{
    public async Task<Result<UnitDto>> Handle(GetUnitByIdQuery request, CancellationToken ct)
    {
        var unit = await units.FindByIdAsync(request.Id, ct);
        if (unit is null)
            return Result<UnitDto>.Failure(Error.NotFound("UnitOfMeasurement", request.Id));

        return Result<UnitDto>.Success(unit.ToDto());
    }
}

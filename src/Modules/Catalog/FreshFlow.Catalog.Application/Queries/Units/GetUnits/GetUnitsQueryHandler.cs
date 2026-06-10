using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.Catalog.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Queries.Units.GetUnits;

internal sealed class GetUnitsQueryHandler(IUnitOfMeasurementRepository units)
    : IRequestHandler<GetUnitsQuery, Result<IReadOnlyList<UnitDto>>>
{
    public async Task<Result<IReadOnlyList<UnitDto>>> Handle(GetUnitsQuery request, CancellationToken ct)
    {
        var rows = await units.GetAllAsync(request.ActiveOnly, ct);
        var dtos = rows.Select(u => u.ToDto()).ToList().AsReadOnly();
        return Result<IReadOnlyList<UnitDto>>.Success(dtos);
    }
}

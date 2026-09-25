using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Caching;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.Catalog.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using FreshFlow.SharedKernel.Caching;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;

namespace FreshFlow.Catalog.Application.Queries.Units.GetUnits;

internal sealed class GetUnitsQueryHandler(IUnitOfMeasurementRepository units, HybridCache cache)
    : IRequestHandler<GetUnitsQuery, Result<IReadOnlyList<UnitDto>>>
{
    public async Task<Result<IReadOnlyList<UnitDto>>> Handle(GetUnitsQuery request, CancellationToken ct)
    {
        var dtos = await cache.GetOrCreateAsync(
            RedisKeys.CatalogUnits(request.ActiveOnly),
            async innerCt =>
            {
                var rows = await units.GetAllAsync(request.ActiveOnly, innerCt);
                return (IReadOnlyList<UnitDto>)rows.Select(u => u.ToDto()).ToList().AsReadOnly();
            },
            CatalogCache.ReferenceData,
            CatalogCache.Units,
            ct);

        return Result<IReadOnlyList<UnitDto>>.Success(dtos);
    }
}

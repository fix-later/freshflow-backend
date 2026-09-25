using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Caching;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.Catalog.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using FreshFlow.SharedKernel.Caching;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;

namespace FreshFlow.Catalog.Application.Queries.PackingCodes.List;

internal sealed class ListPackingCodesQueryHandler(IPackingCodeRepository packingCodes, HybridCache cache)
    : IRequestHandler<ListPackingCodesQuery, Result<IReadOnlyList<PackingCodeDto>>>
{
    public async Task<Result<IReadOnlyList<PackingCodeDto>>> Handle(
        ListPackingCodesQuery request, CancellationToken ct)
    {
        var dtos = await cache.GetOrCreateAsync(
            RedisKeys.CatalogPackingCodes(request.ActiveOnly, request.Page, request.PageSize),
            async innerCt =>
            {
                var rows = await packingCodes.GetPageAsync(
                    request.ActiveOnly, request.Page, request.PageSize, innerCt);
                return (IReadOnlyList<PackingCodeDto>)rows.Select(x => x.ToDto()).ToList().AsReadOnly();
            },
            CatalogCache.ReferenceData,
            CatalogCache.PackingCodes,
            ct);

        return Result<IReadOnlyList<PackingCodeDto>>.Success(dtos);
    }
}

using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Caching;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.Catalog.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using FreshFlow.SharedKernel.Caching;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;

namespace FreshFlow.Catalog.Application.Queries.Categories.GetCategories;

internal sealed class GetCategoriesQueryHandler(IProductCategoryRepository categories, HybridCache cache)
    : IRequestHandler<GetCategoriesQuery, Result<IReadOnlyList<CategoryDto>>>
{
    public async Task<Result<IReadOnlyList<CategoryDto>>> Handle(GetCategoriesQuery request, CancellationToken ct)
    {
        var dtos = await cache.GetOrCreateAsync(
            RedisKeys.CatalogCategories(request.ActiveOnly),
            async innerCt =>
            {
                var rows = await categories.GetAllAsync(request.ActiveOnly, innerCt);
                return (IReadOnlyList<CategoryDto>)rows.Select(c => c.ToDto()).ToList().AsReadOnly();
            },
            CatalogCache.ReferenceData,
            CatalogCache.Categories,
            ct);

        return Result<IReadOnlyList<CategoryDto>>.Success(dtos);
    }
}

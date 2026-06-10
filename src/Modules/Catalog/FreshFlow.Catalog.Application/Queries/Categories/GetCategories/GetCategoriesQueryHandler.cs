using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.Catalog.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Queries.Categories.GetCategories;

internal sealed class GetCategoriesQueryHandler(IProductCategoryRepository categories)
    : IRequestHandler<GetCategoriesQuery, Result<IReadOnlyList<CategoryDto>>>
{
    public async Task<Result<IReadOnlyList<CategoryDto>>> Handle(GetCategoriesQuery request, CancellationToken ct)
    {
        var rows = await categories.GetAllAsync(request.ActiveOnly, ct);
        var dtos = rows.Select(c => c.ToDto()).ToList().AsReadOnly();
        return Result<IReadOnlyList<CategoryDto>>.Success(dtos);
    }
}

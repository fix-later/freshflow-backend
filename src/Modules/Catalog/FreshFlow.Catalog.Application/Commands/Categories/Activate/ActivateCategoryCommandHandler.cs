using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.Catalog.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.Categories.Activate;

internal sealed class ActivateCategoryCommandHandler(IProductCategoryRepository categories)
    : IRequestHandler<ActivateCategoryCommand, Result<CategoryDto>>
{
    public async Task<Result<CategoryDto>> Handle(ActivateCategoryCommand request, CancellationToken ct)
    {
        var category = await categories.FindByIdAsync(request.Id, ct);
        if (category is null)
            return Result<CategoryDto>.Failure(Error.NotFound("Category", request.Id));

        if (category.ParentId is { } parentId)
        {
            var parent = await categories.FindByIdAsync(parentId, ct);
            if (parent is null || !parent.IsActive)
                return Result<CategoryDto>.Failure(
                    Error.Validation("INVALID_CATEGORY_PARENT",
                        "Category parent must be an active root category."));
        }

        category.Activate();
        await categories.SaveChangesAsync(ct);

        return Result<CategoryDto>.Success(category.ToDto());
    }
}

using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.Catalog.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.Categories.Update;

internal sealed class UpdateCategoryCommandHandler(IProductCategoryRepository categories)
    : IRequestHandler<UpdateCategoryCommand, Result<CategoryDto>>
{
    public async Task<Result<CategoryDto>> Handle(UpdateCategoryCommand request, CancellationToken ct)
    {
        var category = await categories.FindByIdAsync(request.Id, ct);
        if (category is null)
            return Result<CategoryDto>.Failure(Error.NotFound("Category", request.Id));

        var name = request.Name.Trim();

        // Skip duplicate-name check if the name is unchanged
        if (!string.Equals(category.Name, name, StringComparison.OrdinalIgnoreCase)
            && await categories.ExistsByNameAsync(name, ct))
            return Result<CategoryDto>.Failure(
                Error.Conflict("CATEGORY_NAME_CONFLICT", $"A category named '{name}' already exists."));

        if (category.ParentId != request.ParentId && request.ParentId is Guid parentId)
        {
            if (parentId == category.Id)
                return Result<CategoryDto>.Failure(
                    Error.Validation("INVALID_CATEGORY_PARENT", "A category cannot be its own parent."));

            var parent = await categories.FindByIdAsync(parentId, ct);
            if (parent is null)
                return Result<CategoryDto>.Failure(
                    new Error("CATEGORY_PARENT_NOT_FOUND", $"Category parent '{parentId}' was not found."));

            if (!parent.IsActive || parent.ParentId is not null)
                return Result<CategoryDto>.Failure(
                    Error.Validation("INVALID_CATEGORY_PARENT", "Category parent must be an active root category."));

            if (await categories.HasChildrenAsync(category.Id, activeOnly: false, ct))
                return Result<CategoryDto>.Failure(
                    Error.Validation("INVALID_CATEGORY_PARENT",
                        "A category with children cannot become a child category."));
        }

        category.Rename(name);
        if (category.ParentId != request.ParentId)
            category.ChangeParent(request.ParentId);
        category.SetImage(request.ImageUrl);
        if (!await categories.SaveChangesAsync(ct))
            return Result<CategoryDto>.Failure(
                Error.Conflict("CATEGORY_NAME_CONFLICT", $"A category named '{name}' already exists."));

        return Result<CategoryDto>.Success(category.ToDto());
    }
}

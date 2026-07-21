using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.Catalog.Application.Mappings;
using FreshFlow.Catalog.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.Categories.Create;

internal sealed class CreateCategoryCommandHandler(IProductCategoryRepository categories)
    : IRequestHandler<CreateCategoryCommand, Result<CategoryDto>>
{
    public async Task<Result<CategoryDto>> Handle(CreateCategoryCommand request, CancellationToken ct)
    {
        if (await categories.ExistsByNameAsync(request.Name, ct))
            return Result<CategoryDto>.Failure(
                Error.Conflict("CATEGORY_NAME_CONFLICT", $"An active category named '{request.Name}' already exists."));

        if (request.ParentId is Guid parentId)
        {
            var parent = await categories.FindByIdAsync(parentId, ct);
            if (parent is null)
                return Result<CategoryDto>.Failure(
                    new Error("CATEGORY_PARENT_NOT_FOUND", $"Category parent '{parentId}' was not found."));

            if (!parent.IsActive || parent.ParentId is not null)
                return Result<CategoryDto>.Failure(
                    Error.Validation("INVALID_CATEGORY_PARENT", "Category parent must be an active root category."));
        }

        var category = new ProductCategory(request.Name, request.ParentId);
        await categories.AddAsync(category, ct);
        await categories.SaveChangesAsync(ct);

        return Result<CategoryDto>.Success(category.ToDto());
    }
}

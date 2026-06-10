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

        var category = new ProductCategory(request.Name);
        await categories.AddAsync(category, ct);
        await categories.SaveChangesAsync(ct);

        return Result<CategoryDto>.Success(category.ToDto());
    }
}

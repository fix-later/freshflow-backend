using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.Catalog.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.Categories.Deactivate;

internal sealed class DeactivateCategoryCommandHandler(IProductCategoryRepository categories)
    : IRequestHandler<DeactivateCategoryCommand, Result<CategoryDto>>
{
    public async Task<Result<CategoryDto>> Handle(DeactivateCategoryCommand request, CancellationToken ct)
    {
        var category = await categories.FindByIdAsync(request.Id, ct);
        if (category is null)
            return Result<CategoryDto>.Failure(Error.NotFound("Category", request.Id));

        category.Deactivate();
        await categories.SaveChangesAsync(ct);

        return Result<CategoryDto>.Success(category.ToDto());
    }
}

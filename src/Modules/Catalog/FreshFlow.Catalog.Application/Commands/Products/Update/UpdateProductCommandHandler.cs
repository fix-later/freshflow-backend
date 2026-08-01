using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.Catalog.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.Products.Update;

internal sealed class UpdateProductCommandHandler(
    IProductRepository products,
    IProductCategoryRepository categories,
    IUnitOfMeasurementRepository units,
    IPackingCodeRepository packingCodes)
    : IRequestHandler<UpdateProductCommand, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(UpdateProductCommand request, CancellationToken ct)
    {
        var product = await products.FindByIdAsync(request.Id, ct);
        if (product is null)
            return Result<ProductDto>.Failure(Error.NotFound("Product", request.Id));

        // Validate unit — must exist and be active
        var unit = await units.FindByIdAsync(request.UnitId, ct);
        if (unit is null || !unit.IsActive)
            return Result<ProductDto>.Failure(
                Error.Validation("INVALID_UNIT",
                    $"Unit '{request.UnitId}' does not exist or is inactive."));

        // Validate category — must exist and be active when provided
        string? legacyCategory = null;
        if (request.CategoryId.HasValue)
        {
            var category = await categories.FindByIdAsync(request.CategoryId.Value, ct);
            if (category is null || !category.IsActive)
                return Result<ProductDto>.Failure(
                    Error.Validation("INVALID_CATEGORY",
                        $"Category '{request.CategoryId}' does not exist or is inactive."));

            legacyCategory = category.Name;
        }

        if (request.PackingCodeId is Guid packingCodeId)
        {
            var packingCode = await packingCodes.FindByIdAsync(packingCodeId, ct);
            if (packingCode is null || !packingCode.IsActive)
                return Result<ProductDto>.Failure(
                    Error.Validation("INVALID_PACKING_CODE",
                        $"Packing code '{packingCodeId}' does not exist or is inactive."));
        }

        product.Update(
            request.Name,
            request.CategoryId,
            request.UnitId,
            request.Description,
            legacyCategory: legacyCategory,
            legacyUnit: unit.Name,
            imageUrl: request.ImageUrl,
            packingCodeId: request.PackingCodeId,
            vatRate: request.VatRate,
            minimumOrderQuantity: request.MinimumOrderQuantity);

        await products.SaveChangesAsync(ct);

        return Result<ProductDto>.Success(product.ToDto());
    }
}

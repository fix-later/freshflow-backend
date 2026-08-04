using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.Catalog.Application.Mappings;
using FreshFlow.Catalog.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.Products.Create;

internal sealed class CreateProductCommandHandler(
    IProductRepository products,
    IProductCategoryRepository categories,
    IUnitOfMeasurementRepository units,
    IPackingCodeRepository packingCodes)
    : IRequestHandler<CreateProductCommand, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(CreateProductCommand request, CancellationToken ct)
    {
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

        PackingCode? packingCode = null;
        if (request.PackingCodeId is Guid packingCodeId)
        {
            packingCode = await packingCodes.FindByIdAsync(packingCodeId, ct);
            if (packingCode is null || !packingCode.IsActive)
                return Result<ProductDto>.Failure(
                    Error.Validation("INVALID_PACKING_CODE",
                        $"Packing code '{packingCodeId}' does not exist or is inactive."));
        }

        var product = new Product(
            request.Name,
            request.UnitId,
            request.CategoryId,
            request.Description,
            request.CreatedBy,
            legacyCategory: legacyCategory,
            legacyUnit: unit.Name,
            packingCodeId: request.PackingCodeId,
            vatRate: request.VatRate,
            minimumOrderQuantity: request.MinimumOrderQuantity);

        await products.AddAsync(product, ct);
        await products.SaveChangesAsync(ct);

        return Result<ProductDto>.Success(product.ToDto(unit, packingCode));
    }
}

using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.Catalog.Domain.Entities;

namespace FreshFlow.Catalog.Application.Mappings;

internal static class ProductMappings
{
    internal static ProductDto ToDto(this Product p) =>
        ToDto(p,
            p.UnitOfMeasurement?.Name ?? p.LegacyUnit ?? string.Empty,
            p.PackingCode?.CapacityKg);

    internal static ProductDto ToDto(
        this Product p,
        UnitOfMeasurement unit,
        PackingCode? packingCode) =>
        ToDto(p, unit.Name, packingCode?.CapacityKg);

    private static ProductDto ToDto(Product p, string unitName, decimal? weightKg) =>
        new(p.Id,
            p.Name,
            p.CategoryId,
            // snapshot value captured at write time; may diverge from current category/unit name
            p.LegacyCategory,
            p.UnitId,
            // snapshot value captured at write time; may diverge from current category/unit name
            p.LegacyUnit,
            p.PackingCodeId,
            p.Description,
            p.CreatedBy,
            p.CreatedAt,
            p.UpdatedAt,
            p.DeletedAt.HasValue,
            p.ImageUrl,
            p.MinimumOrderQuantity,
            p.VatRate,
            new SellingUnitDto(unitName, weightKg));
}

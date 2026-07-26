using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Catalog.Domain.Entities;

public sealed class Product : AggregateRoot
{
    private Product() { } // EF Core

    public Product(
        string name,
        Guid unitId,
        Guid? categoryId,
        string? description,
        Guid? createdBy,
        string? legacyCategory = null,
        string? legacyUnit = null,
        Guid? packingCodeId = null,
        string? vatRate = null)
    {
        Name = name;
        UnitId = unitId;
        CategoryId = categoryId;
        Description = description;
        CreatedBy = createdBy;
        LegacyCategory = legacyCategory;
        LegacyUnit = legacyUnit;
        PackingCodeId = packingCodeId;
        VatRate = vatRate;
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>FK → product_categories (nullable; null = uncategorised).</summary>
    public Guid? CategoryId { get; private set; }

    /// <summary>FK → units_of_measurement.</summary>
    public Guid UnitId { get; private set; }

    /// <summary>FK → packing_codes (nullable; null = no packing code assigned).</summary>
    public Guid? PackingCodeId { get; private set; }

    public string? Description { get; private set; }

    /// <summary>FK → users (nullable; null = system-created).</summary>
    public Guid? CreatedBy { get; private set; }

    /// <summary>Legacy free-text category kept for transition; superseded by CategoryId.</summary>
    public string? LegacyCategory { get; private set; }

    /// <summary>Legacy free-text unit kept for transition; superseded by UnitId.</summary>
    public string? LegacyUnit { get; private set; }

    public string? ImageUrl { get; private set; }

    /// <summary>
    /// VAT rate code for invoicing: "KCT" (không chịu thuế), "0", "5", "8" or "10". Null = not
    /// configured (Invoicing treats null as KCT for v1). Fresh food is not uniformly 10%.
    /// </summary>
    public string? VatRate { get; private set; }

    public void Update(
        string name,
        Guid? categoryId,
        Guid unitId,
        string? description,
        string? legacyCategory = null,
        string? legacyUnit = null,
        string? imageUrl = null,
        Guid? packingCodeId = null,
        string? vatRate = null)
    {
        Name = name;
        CategoryId = categoryId;
        UnitId = unitId;
        Description = description;
        LegacyCategory = legacyCategory;
        LegacyUnit = legacyUnit;
        PackingCodeId = packingCodeId;
        if (imageUrl is not null)
            ImageUrl = imageUrl;
        if (vatRate is not null)
            VatRate = vatRate;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Delete()
    {
        SoftDelete();
        UpdatedAt = DateTime.UtcNow;
    }
}

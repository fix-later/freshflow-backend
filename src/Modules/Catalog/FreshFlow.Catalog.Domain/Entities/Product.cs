using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Catalog.Domain.Entities;

public sealed class Product : AggregateRoot
{
    private Product() { } // EF Core

    public Product(
        string name,
        Guid? categoryId,
        Guid unitId,
        string? description,
        Guid? createdBy,
        string? legacyCategory = null,
        string? legacyUnit = null)
    {
        Name = name;
        CategoryId = categoryId;
        UnitId = unitId;
        Description = description;
        CreatedBy = createdBy;
        LegacyCategory = legacyCategory;
        LegacyUnit = legacyUnit;
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>FK → product_categories (nullable; null = uncategorised).</summary>
    public Guid? CategoryId { get; private set; }

    /// <summary>FK → units_of_measurement.</summary>
    public Guid UnitId { get; private set; }

    public string? Description { get; private set; }

    /// <summary>FK → users (nullable; null = system-created).</summary>
    public Guid? CreatedBy { get; private set; }

    /// <summary>Legacy free-text category kept for transition; superseded by CategoryId.</summary>
    public string? LegacyCategory { get; private set; }

    /// <summary>Legacy free-text unit kept for transition; superseded by UnitId.</summary>
    public string? LegacyUnit { get; private set; }

    public void Update(string name, Guid? categoryId, Guid unitId, string? description)
    {
        Name = name;
        CategoryId = categoryId;
        UnitId = unitId;
        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Delete()
    {
        SoftDelete();
        UpdatedAt = DateTime.UtcNow;
    }
}

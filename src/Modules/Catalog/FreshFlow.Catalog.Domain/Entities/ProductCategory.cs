using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Catalog.Domain.Entities;

public sealed class ProductCategory : AggregateRoot
{
    private ProductCategory() { } // EF Core

    public ProductCategory(string name)
    {
        Name = ValidateName(name);
        IsActive = true;
    }

    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    public void Rename(string name)
    {
        Name = ValidateName(name);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Delete()
    {
        SoftDelete();
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    // ── Invariant helpers ────────────────────────────────────────────────────

    private static string ValidateName(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
            throw new ArgumentException("Category name must not be blank.", nameof(name));
        return trimmed;
    }
}

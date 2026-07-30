using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Catalog.Domain.Entities;

public sealed class ProductCategory : AggregateRoot
{
    private ProductCategory() { } // EF Core

    public ProductCategory(string name, Guid? parentId = null, string? imageUrl = null)
    {
        Name = ValidateName(name);
        ParentId = parentId;
        ImageUrl = imageUrl;
        IsActive = true;
    }

    public string Name { get; private set; } = string.Empty;
    public Guid? ParentId { get; private set; }
    public string? ImageUrl { get; private set; }
    public bool IsActive { get; private set; } = true;

    public void Rename(string name)
    {
        Name = ValidateName(name);
        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangeParent(Guid? parentId)
    {
        ParentId = parentId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetImage(string? imageUrl)
    {
        ImageUrl = imageUrl;
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

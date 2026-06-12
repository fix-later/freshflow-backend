namespace FreshFlow.Pricing.Infrastructure.CrossModule;

/// <summary>
/// Infrastructure-only EF entity — read-only projection onto the products table
/// owned by the Catalog module. Pricing reads Id, Name, and IsActive for listing
/// without a cross-module project reference.
/// </summary>
internal sealed class ProductRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

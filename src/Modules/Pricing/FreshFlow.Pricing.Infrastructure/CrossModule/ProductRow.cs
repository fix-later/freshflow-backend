namespace FreshFlow.Pricing.Infrastructure.CrossModule;

/// <summary>
/// Infrastructure-only EF entity — read-only projection onto the products table
/// owned by the Catalog module. Pricing reads Id and Name for listing without a
/// cross-module project reference. Catalog.Product has no "active" flag — it uses
/// soft-delete (DeletedAt) only, which the backing query already filters on.
/// </summary>
internal sealed class ProductRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

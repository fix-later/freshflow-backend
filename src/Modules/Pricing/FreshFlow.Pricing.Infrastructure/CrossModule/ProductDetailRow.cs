namespace FreshFlow.Pricing.Infrastructure.CrossModule;

/// <summary>
/// Infrastructure-only EF entity — enriched read-only projection onto the products table,
/// joined with units_of_measurement and product_categories owned by the Catalog module.
/// Deleted products are excluded by the SQL query ("WHERE p.DeletedAt IS NULL").
/// Pricing reads Id, Name, Unit, and Category for market product listings
/// without a direct cross-module project reference.
/// </summary>
internal sealed class ProductDetailRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string? Category { get; set; }
}

namespace FreshFlow.Pricing.Infrastructure.CrossModule;

/// <summary>
/// Infrastructure-only EF entity — read-only projection onto the markets table
/// owned by the Catalog module. Pricing reads Id, Name, Location, Address, and IsActive
/// for listing and validation without a cross-module project reference.
/// </summary>
internal sealed class MarketRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; }
}

namespace FreshFlow.Orders.Infrastructure.CrossModule;

/// <summary>
/// Infrastructure-only EF entity — read-only projection onto the restaurants table owned by
/// the Auth module. Orders reads Id, UserId, and approval status without a cross-module
/// project reference.
/// </summary>
internal sealed class RestaurantRow
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Status { get; set; } = string.Empty;
}

internal sealed class DeliveryAddressRow
{
    public Guid Id { get; set; }
    public Guid RestaurantId { get; set; }
    public string? RecipientName { get; set; }
    public string? Phone { get; set; }
    public string AddressLine { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}

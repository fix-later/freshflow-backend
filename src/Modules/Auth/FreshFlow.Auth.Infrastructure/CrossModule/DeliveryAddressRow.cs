namespace FreshFlow.Auth.Infrastructure.CrossModule;

// Infrastructure-only EF entity — maps to delivery_addresses table.
// Scoped to Auth module; ownership is always resolved via RestaurantRow.UserId.
internal sealed class DeliveryAddressRow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public string? RecipientName { get; set; }
    public string? Phone { get; set; }
    public string AddressLine { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
}

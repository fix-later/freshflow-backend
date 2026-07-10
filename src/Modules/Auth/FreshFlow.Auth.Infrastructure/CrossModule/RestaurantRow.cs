using FreshFlow.Auth.Domain.Enums;

namespace FreshFlow.Auth.Infrastructure.CrossModule;

// Infrastructure-only EF entity — maps to the restaurants table that Orders module owns.
// Auth creates minimal rows here during user creation; Orders will extend this table.
internal sealed class RestaurantRow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? ContactPerson { get; set; }
    public TimeOnly? PickupStart { get; set; }
    public TimeOnly? PickupEnd { get; set; }
    public string? BusinessLicenseUrl { get; set; }
    public RestaurantStatus Status { get; set; } = RestaurantStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

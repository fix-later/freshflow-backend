namespace FreshFlow.Auth.Infrastructure.CrossModule;

// Infrastructure-only EF entity — maps to the restaurants table that Orders module owns.
// Auth creates minimal rows here during user creation; Orders will extend this table.
internal sealed class RestaurantRow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

namespace FreshFlow.Auth.Domain.Entities;

/// <summary>
/// Lookup entity representing a named role in the system.
/// Rows are seeded at startup; this table is append-only in normal operation.
/// </summary>
public sealed class Role
{
    private Role() { }  // EF Core materialisation

    public Role(string name, string description)
    {
        Id = Guid.NewGuid();
        Name = name;
        Description = description;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
}

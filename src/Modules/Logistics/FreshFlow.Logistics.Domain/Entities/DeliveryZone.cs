namespace FreshFlow.Logistics.Domain.Entities;

public sealed class DeliveryZone
{
    private DeliveryZone() { } // EF Core

    public DeliveryZone(string code, string name, string? description)
    {
        Code = NormalizeCode(code);
        Name = NormalizeName(name);
        Description = NormalizeOptional(description);
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public void Update(string name, string? description)
    {
        Name = NormalizeName(name);
        Description = NormalizeOptional(description);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        if (DeletedAt is not null)
            return;

        DeletedAt = DateTime.UtcNow;
        IsActive = false;
        UpdatedAt = DeletedAt.Value;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string NormalizeCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Delivery zone code is required.", nameof(code));

        return code.Trim().ToUpperInvariant();
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Delivery zone name is required.", nameof(name));

        return name.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}

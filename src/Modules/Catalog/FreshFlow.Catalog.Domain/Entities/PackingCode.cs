using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Catalog.Domain.Entities;

public sealed class PackingCode : AggregateRoot
{
    private PackingCode() { } // EF Core

    public PackingCode(string code, string? description, decimal capacityKg)
    {
        Validate(code, capacityKg);
        Code = code.Trim();
        Description = description?.Trim();
        CapacityKg = capacityKg;
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    /// <summary>Weight of product (kg) one box holds.</summary>
    public decimal CapacityKg { get; private set; }

    public bool IsActive { get; private set; } = true;

    public void Update(string code, string? description, decimal capacityKg)
    {
        Validate(code, capacityKg);
        Code = code.Trim();
        Description = description?.Trim();
        CapacityKg = capacityKg;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Delete()
    {
        SoftDelete();
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    private static void Validate(string code, decimal capacityKg)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code is required.", nameof(code));
        if (capacityKg <= 0)
            throw new ArgumentException("CapacityKg must be > 0.", nameof(capacityKg));
        if (capacityKg != decimal.Truncate(capacityKg))
            throw new ArgumentException("CapacityKg must be a whole number of kilograms.", nameof(capacityKg));
    }
}

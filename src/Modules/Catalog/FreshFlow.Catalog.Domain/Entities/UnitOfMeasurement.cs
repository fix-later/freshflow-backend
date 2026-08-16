using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Catalog.Domain.Entities;

public sealed class UnitOfMeasurement : AggregateRoot
{
    private UnitOfMeasurement() { } // EF Core

    public UnitOfMeasurement(string name, string? abbreviation)
    {
        Name = name;
        Abbreviation = abbreviation;
        IsActive = true;
    }

    public string Name { get; private set; } = string.Empty;
    public string? Abbreviation { get; private set; }
    public bool IsActive { get; private set; } = true;

    public void Update(string name, string? abbreviation)
    {
        Name = name;
        Abbreviation = abbreviation;
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
}

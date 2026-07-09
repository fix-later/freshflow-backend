using FreshFlow.Logistics.Domain.Enums;

namespace FreshFlow.Logistics.Domain.Entities;

public sealed class Vehicle
{
    private Vehicle() { } // EF Core

    public Vehicle(
        string plateNumber,
        decimal capacityKg,
        VehicleType vehicleType,
        Guid? registeredBy)
    {
        Validate(plateNumber, capacityKg);

        Id = Guid.NewGuid();
        PlateNumber = plateNumber.Trim();
        CapacityKg = capacityKg;
        VehicleType = vehicleType;
        RegisteredBy = registeredBy;
        IsAvailable = true;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; }
    public string PlateNumber { get; private set; } = string.Empty;
    public decimal CapacityKg { get; private set; }
    public VehicleType VehicleType { get; private set; }
    public bool IsAvailable { get; private set; }
    public Guid? RegisteredBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public void Update(string plateNumber, decimal capacityKg, VehicleType vehicleType)
    {
        Validate(plateNumber, capacityKg);

        PlateNumber = plateNumber.Trim();
        CapacityKg = capacityKg;
        VehicleType = vehicleType;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        if (DeletedAt is not null)
            return;

        DeletedAt = DateTime.UtcNow;
        UpdatedAt = DeletedAt.Value;
    }

    public void MarkAvailable()
    {
        IsAvailable = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkUnavailable()
    {
        IsAvailable = false;
        UpdatedAt = DateTime.UtcNow;
    }

    private static void Validate(string plateNumber, decimal capacityKg)
    {
        if (string.IsNullOrWhiteSpace(plateNumber))
            throw new ArgumentException("Plate number is required.", nameof(plateNumber));

        if (capacityKg <= 0)
            throw new ArgumentException("Capacity must be greater than zero.", nameof(capacityKg));
    }
}

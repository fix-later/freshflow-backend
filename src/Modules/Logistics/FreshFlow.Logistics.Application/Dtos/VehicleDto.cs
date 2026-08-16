namespace FreshFlow.Logistics.Application.Dtos;

public sealed record VehicleDto(
    Guid Id,
    string PlateNumber,
    decimal CapacityKg,
    string VehicleType,
    bool IsAvailable,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    Guid? HubId = null);

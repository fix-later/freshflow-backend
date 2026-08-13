namespace FreshFlow.Procurement.Application.Abstractions;

public interface IMarketSessionReadinessReader
{
    public Task<VehicleAvailabilityDto> ReadVehicleAvailabilityAsync(
        Guid hubId, DateOnly serviceDate, CancellationToken ct);
}

public sealed record VehicleAvailabilityDto(
    int Count,
    decimal CapacityKg,
    IReadOnlyList<MarketSessionVehicleOptionDto>? Vehicles = null);

public sealed record MarketSessionVehicleOptionDto(
    Guid VehicleId,
    string PlateNumber,
    decimal CapacityKg,
    string VehicleType,
    bool IsAvailable);

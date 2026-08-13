namespace FreshFlow.Procurement.Application.Abstractions;

public interface IMarketSessionReadinessReader
{
    public Task<VehicleAvailabilityDto> ReadVehicleAvailabilityAsync(
        Guid hubId, DateOnly serviceDate, CancellationToken ct);
}

public sealed record VehicleAvailabilityDto(int Count, decimal CapacityKg);

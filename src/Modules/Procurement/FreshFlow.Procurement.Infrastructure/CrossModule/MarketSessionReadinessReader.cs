using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Procurement.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Procurement.Infrastructure.CrossModule;

internal sealed class MarketSessionReadinessReader(AppDbContext db) : IMarketSessionReadinessReader
{
    public async Task<VehicleAvailabilityDto> ReadVehicleAvailabilityAsync(
        Guid hubId, DateOnly serviceDate, CancellationToken ct)
    {
        var reservedIds = db.Set<ReservedRouteVehicleRow>()
            .Where(route => route.ServiceDate == serviceDate)
            .Select(route => route.VehicleId);
        var available = db.Set<MarketSessionVehicleRow>()
            .Where(vehicle =>
                vehicle.HubId == hubId &&
                vehicle.IsAvailable &&
                vehicle.DeletedAt == null &&
                !reservedIds.Contains(vehicle.Id));

        return new VehicleAvailabilityDto(
            await available.CountAsync(ct),
            await available.SumAsync(vehicle => vehicle.CapacityKg, ct),
            await available
                .OrderBy(vehicle => vehicle.PlateNumber)
                .Select(vehicle => new MarketSessionVehicleOptionDto(
                    vehicle.Id, vehicle.PlateNumber, vehicle.CapacityKg, vehicle.VehicleType, true))
                .ToListAsync(ct));
    }
}

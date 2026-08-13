namespace FreshFlow.Logistics.Application.Abstractions;

public interface IMarketSessionVehicleReader
{
    public Task<IReadOnlySet<Guid>> ReadAssignedVehicleIdsAsync(
        Guid hubId, DateOnly serviceDate, CancellationToken ct);
}

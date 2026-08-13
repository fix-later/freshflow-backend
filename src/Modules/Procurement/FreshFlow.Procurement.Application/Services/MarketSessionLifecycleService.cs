using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;

namespace FreshFlow.Procurement.Application.Services;

public sealed class MarketSessionLifecycleService(
    IMarketSessionRepository sessions,
    IMarketCodeReader markets,
    IHubByMarketReader hubs,
    IMarketAgentReader agents,
    IMarketSessionReadinessReader vehicles,
    IOperationalSettingsReader settings,
    TimeProvider timeProvider)
{
    public async Task<MarketSessionReadiness> ReadReadinessAsync(
        Guid marketId, Guid? hubId, DateOnly serviceDate, CancellationToken ct)
    {
        var operational = await settings.ReadAsync(ct);
        var agentCount = await agents.CountEligibleMarketAgentsAsync(marketId, ct);
        var vehicleAvailability = hubId.HasValue
            ? await vehicles.ReadVehicleAvailabilityAsync(hubId.Value, serviceDate, ct)
            : new VehicleAvailabilityDto(0, 0m);
        return new MarketSessionReadiness(
            operational.BatchingEnabled,
            hubId.HasValue,
            agentCount,
            vehicleAvailability.Count,
            vehicleAvailability.CapacityKg,
            vehicleAvailability.CapacityKg);
    }

    public async Task<MarketSessionReadiness> ReadReadinessAsync(MarketSession session, CancellationToken ct)
    {
        var live = await ReadReadinessAsync(session.MarketId, session.HubId, session.ServiceDate, ct);
        if (session.Vehicles.Count == 0 && session.Agents.Count == 0)
            return live;

        var vehicleIds = session.Vehicles.Select(row => row.VehicleId).ToHashSet();
        var selectedVehicles = (session.HubId.HasValue
                ? await vehicles.ReadVehicleAvailabilityAsync(session.HubId.Value, session.ServiceDate, ct)
                : new VehicleAvailabilityDto(0, 0m))
            .Vehicles?.Where(vehicle => vehicleIds.Contains(vehicle.VehicleId)).ToList() ?? [];
        var eligibleAgentIds = (await agents.ListEligibleMarketAgentsAsync(session.MarketId, ct))
            .Select(agent => agent.UserId)
            .ToHashSet();
        var selectedAgentCount = session.Agents.Count(row => eligibleAgentIds.Contains(row.UserId));
        return new MarketSessionReadiness(
            live.BatchingEnabled,
            live.HasHub,
            selectedAgentCount,
            selectedVehicles.Count,
            selectedVehicles.Sum(vehicle => vehicle.CapacityKg),
            live.ReferenceVehicleCapacityKg);
    }

    public async Task EnsureRollingWindowAsync(CancellationToken ct)
    {
        var operational = await settings.ReadAsync(ct);
        var today = ProcurementBatchCycle.GetLocalDate(timeProvider.GetUtcNow());
        var from = today.AddDays(1);
        var to = today.AddDays(Math.Max(1, operational.DeliveryWindowDays));
        var marketIds = await markets.ListActiveMarketIdsAsync(ct);
        var hubByMarket = await hubs.ReadActiveHubsAsync(marketIds, ct);
        var existing = await sessions.ListKeysAsync(from, to, ct);

        foreach (var marketId in marketIds)
            foreach (var serviceDate in Dates(from, to))
            {
                if (existing.Contains((marketId, serviceDate)))
                    continue;

                hubByMarket.TryGetValue(marketId, out var hubId);
                Guid? resolvedHubId = hubId == Guid.Empty ? null : hubId;
                var readiness = await ReadReadinessAsync(marketId, resolvedHubId, serviceDate, ct);
                var create = MarketSession.Create(
                    marketId,
                    resolvedHubId,
                    serviceDate,
                    ProcurementBatchCycle.GetCloseAtUtc(serviceDate, operational.DailyCutoffTime),
                    MarketSessionCreatedSource.Auto,
                    readiness.IsReady);
                if (create.IsFailure)
                    continue;

                var eligibleAgents = await agents.ListEligibleMarketAgentsAsync(marketId, ct);
                if (resolvedHubId.HasValue)
                {
                    var availableVehicles = await vehicles.ReadVehicleAvailabilityAsync(
                        resolvedHubId.Value, serviceDate, ct);
                    if (availableVehicles.Vehicles is { Count: > 0 } && eligibleAgents.Count > 0)
                    {
                        create.Value.ConfigureResources(
                            availableVehicles.CapacityKg,
                            availableVehicles.Vehicles.Select(vehicle => vehicle.VehicleId),
                            eligibleAgents.Select(agent => agent.UserId),
                            null,
                            timeProvider.GetUtcNow().UtcDateTime);
                    }
                }

                await sessions.AddAsync(create.Value, ct);
                await sessions.SaveChangesAsync(ct); // unique violation means another instance won
            }
    }

    public async Task CloseDueAsync(CancellationToken ct)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var due = await sessions.ListDueAsync(nowUtc, ct);
        foreach (var session in due)
            session.Close(null, "cutoff_reached", nowUtc);
        if (due.Count > 0)
            await sessions.SaveChangesAsync(ct);
    }

    private static IEnumerable<DateOnly> Dates(DateOnly from, DateOnly to)
    {
        for (var date = from; date <= to; date = date.AddDays(1))
            yield return date;
    }
}

public sealed record MarketSessionReadiness(
    bool BatchingEnabled,
    bool HasHub,
    int EligibleAgentCount,
    int AvailableVehicleCount,
    decimal HubVehicleCapacityKg,
    decimal ReferenceVehicleCapacityKg)
{
    public bool IsReady => BatchingEnabled && HasHub && EligibleAgentCount > 0 && AvailableVehicleCount > 0;

    public IReadOnlyList<string> Warnings
    {
        get
        {
            var warnings = new List<string>();
            if (!BatchingEnabled) warnings.Add("BATCHING_DISABLED");
            if (!HasHub) warnings.Add("HUB_NOT_CONFIGURED");
            if (EligibleAgentCount == 0) warnings.Add("NO_MARKET_AGENT");
            if (AvailableVehicleCount == 0) warnings.Add("NO_VEHICLE");
            return warnings;
        }
    }
}

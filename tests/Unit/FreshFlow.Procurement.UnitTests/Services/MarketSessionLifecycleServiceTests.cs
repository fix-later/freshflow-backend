using FluentAssertions;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Services;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using NSubstitute;

namespace FreshFlow.Procurement.UnitTests.Services;

[Trait("Category", "Unit")]
public sealed class MarketSessionLifecycleServiceTests
{
    [Fact]
    public async Task EnsureRollingWindow_CreatesD1ThroughD7WithoutOverwritingExistingAsync()
    {
        var marketId = Guid.NewGuid();
        var hubId = Guid.NewGuid();
        var sessions = Substitute.For<IMarketSessionRepository>();
        sessions.ListKeysAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), default)
            .Returns(new HashSet<(Guid, DateOnly)> { (marketId, new DateOnly(2026, 8, 14)) });
        sessions.SaveChangesAsync(default).Returns(true);
        var created = new List<MarketSession>();
        sessions.AddAsync(Arg.Do<MarketSession>(created.Add), default).Returns(Task.CompletedTask);
        var markets = Substitute.For<IMarketCodeReader>();
        markets.ListActiveMarketIdsAsync(default).Returns([marketId]);
        var hubs = Substitute.For<IHubByMarketReader>();
        hubs.ReadActiveHubsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), default)
            .Returns(new Dictionary<Guid, Guid> { [marketId] = hubId });
        var agents = Substitute.For<IMarketAgentReader>();
        agents.CountEligibleMarketAgentsAsync(marketId, default).Returns(1);
        var agentId = Guid.NewGuid();
        agents.ListEligibleMarketAgentsAsync(marketId, default)
            .Returns([new MarketAgentOptionDto(agentId, "agent@test.local", "Agent")]);
        var vehicles = Substitute.For<IMarketSessionReadinessReader>();
        var vehicleId = Guid.NewGuid();
        vehicles.ReadVehicleAvailabilityAsync(hubId, Arg.Any<DateOnly>(), default)
            .Returns(new VehicleAvailabilityDto(1, 500m,
                [new MarketSessionVehicleOptionDto(vehicleId, "51A-001", 500m, "van", true)]));
        var settings = Substitute.For<IOperationalSettingsReader>();
        settings.ReadAsync(default)
            .Returns(new ProcurementOperationalSettingsDto(true, new TimeOnly(22, 0), 7));
        var service = new MarketSessionLifecycleService(
            sessions, markets, hubs, agents, vehicles, settings,
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 13, 3, 0, 0, TimeSpan.Zero)));

        await service.EnsureRollingWindowAsync(default);

        created.Select(session => session.ServiceDate).Should().Equal(
            Enumerable.Range(15, 6).Select(day => new DateOnly(2026, 8, day)));
        created.Should().OnlyContain(session =>
            session.Status == MarketSessionStatus.Open && session.HubId == hubId);
        created.Should().OnlyContain(session =>
            session.PlannedCapacityKg == 500m &&
            session.Vehicles.Single().VehicleId == vehicleId &&
            session.Agents.Single().UserId == agentId);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

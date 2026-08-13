using FluentAssertions;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.Procurement.Application.Queries.GetMarketSessionTracking;
using FreshFlow.Procurement.Application.Services;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using NSubstitute;

namespace FreshFlow.Procurement.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetMarketSessionTrackingQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsExactTrackingSnapshotAsync()
    {
        var marketId = Guid.NewGuid();
        var hubId = Guid.NewGuid();
        var session = MarketSession.Create(
            marketId,
            hubId,
            new DateOnly(2026, 8, 14),
            new DateTime(2026, 8, 13, 15, 0, 0, DateTimeKind.Utc),
            MarketSessionCreatedSource.Auto,
            true).Value;
        var sessions = Substitute.For<IMarketSessionRepository>();
        sessions.FindByIdAsync(session.Id, default).Returns(session);
        var markets = Substitute.For<IMarketCodeReader>();
        markets.ReadMarketCodesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), default)
            .Returns(new Dictionary<Guid, (string? Code, string Name)>
            {
                [marketId] = ("TD", "Thu Duc")
            });
        var agents = Substitute.For<IMarketAgentReader>();
        agents.CountEligibleMarketAgentsAsync(marketId, default).Returns(2);
        var vehicles = Substitute.For<IMarketSessionReadinessReader>();
        vehicles.ReadVehicleAvailabilityAsync(hubId, session.ServiceDate, default)
            .Returns(new VehicleAvailabilityDto(1, 500m));
        var settings = Substitute.For<IOperationalSettingsReader>();
        settings.ReadAsync(default)
            .Returns(new ProcurementOperationalSettingsDto(true, new TimeOnly(22, 0)));
        var lifecycle = new MarketSessionLifecycleService(
            sessions,
            markets,
            Substitute.For<IHubByMarketReader>(),
            agents,
            vehicles,
            settings,
            TimeProvider.System);
        var snapshot = new MarketSessionTrackingData(
            new MarketSessionTrackingSummaryDto(2, 1, 1, 3, 9),
            [],
            [],
            new ProcurementBatchPaginationDto(2, 1, 50),
            null);
        var tracking = Substitute.For<IMarketSessionTrackingReader>();
        tracking.ReadAsync(session.Id, 1, 50, default).Returns(snapshot);
        var handler = new GetMarketSessionTrackingQueryHandler(
            sessions, markets, lifecycle, tracking);

        var result = await handler.Handle(
            new GetMarketSessionTrackingQuery(session.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Session.Id.Should().Be(session.Id);
        result.Value.Summary.Should().Be(snapshot.Summary);
        result.Value.OrdersPagination.Total.Should().Be(2);
    }
}

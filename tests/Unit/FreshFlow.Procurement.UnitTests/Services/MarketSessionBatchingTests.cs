using FluentAssertions;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Services;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using NSubstitute;

namespace FreshFlow.Procurement.UnitTests.Services;

[Trait("Category", "Unit")]
public sealed class MarketSessionBatchingTests
{
    [Fact]
    public async Task ClosedEmptySession_WithoutHub_IsCompletedWithoutCreatingBatchAsync()
    {
        var marketId = Guid.NewGuid();
        var session = MarketSession.Create(
            marketId,
            null,
            new DateOnly(2026, 8, 20),
            new DateTime(2026, 8, 19, 15, 0, 0, DateTimeKind.Utc),
            MarketSessionCreatedSource.Auto,
            false).Value;
        session.Close(null, null, DateTime.UtcNow);
        var orders = Substitute.For<IConfirmedOrderReader>();
        orders.ReadEligibleForSessionAsync(session.Id, default).Returns([]);
        var sessions = Substitute.For<IMarketSessionRepository>();
        sessions.FindByIdAsync(session.Id, default).Returns(session);
        sessions.SaveChangesAsync(default).Returns(true);
        var settings = Substitute.For<IOperationalSettingsReader>();
        settings.ReadAsync(default).Returns(new ProcurementOperationalSettingsDto(true, new TimeOnly(22, 0)));
        var batches = Substitute.For<IProcurementBatchRepository>();
        var service = new BatchConfirmedOrdersService(
            orders,
            Substitute.For<IMarketCodeReader>(),
            settings,
            batches,
            TimeProvider.System,
            sessions,
            Substitute.For<IHubByMarketReader>());

        var result = await service.BuildSessionBatchAsync(session.Id, false, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Reason.Should().Be("no_eligible_orders");
        session.BatchingCompletedAt.Should().NotBeNull();
        await batches.DidNotReceive().AddRangeAsync(
            Arg.Any<IReadOnlyCollection<ProcurementBatch>>(), default);
    }

    [Fact]
    public async Task ClosedSession_WithHubLinkedAfterSessionCreated_ResolvesHubLiveAndBatchesAsync()
    {
        var marketId = Guid.NewGuid();
        var hubId = Guid.NewGuid();
        var marketProductId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var session = MarketSession.Create(
            marketId,
            null, // hub did not exist yet when the session was auto-created
            new DateOnly(2026, 8, 20),
            new DateTime(2026, 8, 19, 15, 0, 0, DateTimeKind.Utc),
            MarketSessionCreatedSource.Auto,
            false).Value;
        session.Close(null, null, DateTime.UtcNow);
        var eligibleOrder = new ConfirmedOrderDto(
            orderId,
            DateTime.UtcNow,
            [new ConfirmedOrderItemDto(marketProductId, "Rau muống", 5)]);
        var orders = Substitute.For<IConfirmedOrderReader>();
        orders.ReadEligibleForSessionAsync(session.Id, default).Returns([eligibleOrder]);
        var sessions = Substitute.For<IMarketSessionRepository>();
        sessions.FindByIdAsync(session.Id, default).Returns(session);
        sessions.SaveChangesAsync(default).Returns(true);
        var settings = Substitute.For<IOperationalSettingsReader>();
        settings.ReadAsync(default).Returns(new ProcurementOperationalSettingsDto(true, new TimeOnly(22, 0)));
        var batches = Substitute.For<IProcurementBatchRepository>();
        batches.CountByMarketAndDateAsync(marketId, session.ServiceDate, default).Returns(0);
        batches.SaveChangesAsync(default).Returns(true);
        var marketCodes = Substitute.For<IMarketCodeReader>();
        marketCodes.ReadMarketCodesAsync(Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(marketId)), default)
            .Returns(new Dictionary<Guid, (string? Code, string Name)> { [marketId] = ("BT", "Chợ Bến Thành") });
        var hubs = Substitute.For<IHubByMarketReader>();
        hubs.ReadActiveHubsAsync(Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(marketId)), default)
            .Returns(new Dictionary<Guid, Guid> { [marketId] = hubId });
        var service = new BatchConfirmedOrdersService(
            orders,
            marketCodes,
            settings,
            batches,
            TimeProvider.System,
            sessions,
            hubs);

        var result = await service.BuildSessionBatchAsync(session.Id, false, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Reason.Should().BeNull();
        await batches.Received(1).AddRangeAsync(
            Arg.Is<IReadOnlyCollection<ProcurementBatch>>(built => built.Single().HubId == hubId), default);
    }
}

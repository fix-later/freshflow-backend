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
        orders.ReadEligibleForMarketAsync(session.ServiceDate, marketId, default).Returns([]);
        var sessions = Substitute.For<IMarketSessionRepository>();
        sessions.FindByIdAsync(session.Id, default).Returns(session);
        sessions.SaveChangesAsync(default).Returns(true);
        var settings = Substitute.For<IOperationalSettingsReader>();
        settings.ReadAsync(default).Returns(new ProcurementOperationalSettingsDto(true, new TimeOnly(22, 0)));
        var batches = Substitute.For<IProcurementBatchRepository>();
        var service = new BatchConfirmedOrdersService(
            orders,
            Substitute.For<IMarketProductMarketReader>(),
            Substitute.For<IHubByMarketReader>(),
            Substitute.For<IMarketCodeReader>(),
            settings,
            batches,
            TimeProvider.System,
            sessions);

        var result = await service.BuildSessionBatchAsync(session.Id, false, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Reason.Should().Be("no_eligible_orders");
        session.BatchingCompletedAt.Should().NotBeNull();
        await batches.DidNotReceive().AddRangeAsync(
            Arg.Any<IReadOnlyCollection<ProcurementBatch>>(), default);
    }
}

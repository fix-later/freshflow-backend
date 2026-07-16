using System.Text.Json;
using FluentAssertions;
using FreshFlow.Contracts;
using FreshFlow.Infrastructure.Persistence.Audit;
using FreshFlow.SharedKernel.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FreshFlow.Infrastructure.Persistence.UnitTests.Audit;

[Trait("Category", "Unit")]
public sealed class AuditLogHandlersTests
{
    private readonly IAuditLogWriter _writer = Substitute.For<IAuditLogWriter>();
    private static readonly DateTime OccurredAt = new(2026, 6, 18, 7, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task OrderCancelledHandler_WritesOrderCancelledActionAsync()
    {
        var orderId = Guid.NewGuid();
        var sut = new OrderCancelledAuditLogHandler(_writer);

        await sut.Handle(
            new OrderCancelledIntegrationEvent(orderId, Guid.NewGuid(), "out of stock", OccurredAt), default);

        await _writer.Received(1).WriteAsync(
            null, "order_cancelled", "order", orderId, Arg.Any<string>(), OccurredAt, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreditLimitThresholdReachedHandler_WritesCreditLimitActionAsync()
    {
        var restaurantId = Guid.NewGuid();
        var sut = new CreditLimitThresholdReachedAuditLogHandler(_writer);

        await sut.Handle(
            new CreditLimitThresholdReachedIntegrationEvent(restaurantId, "warning", 0.8m, 80m, 100m, OccurredAt),
            default);

        await _writer.Received(1).WriteAsync(
            null, "credit_limit_threshold_reached", "restaurant", restaurantId,
            Arg.Any<string>(), OccurredAt, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RestaurantRefundIssuedHandler_WritesRefundActionAsync()
    {
        var restaurantId = Guid.NewGuid();
        var sut = new RestaurantRefundIssuedAuditLogHandler(_writer);

        await sut.Handle(
            new RestaurantRefundIssuedIntegrationEvent(
                restaurantId, Guid.NewGuid(), "Cà chua", 2m, 40_000m, OccurredAt),
            default);

        await _writer.Received(1).WriteAsync(
            null, "restaurant_refund_issued", "restaurant", restaurantId,
            Arg.Any<string>(), OccurredAt, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PriceUpdatedHandler_WritesPriceUpdatedActionWithActorAsync()
    {
        var marketProductId = Guid.NewGuid();
        var updatedBy = Guid.NewGuid();
        var sut = new PriceUpdatedAuditLogHandler(_writer);

        await sut.Handle(
            new PriceUpdatedIntegrationEvent(
                marketProductId, Guid.NewGuid(), Guid.NewGuid(), 10_000m, 12_000m, 50, updatedBy, OccurredAt),
            default);

        await _writer.Received(1).WriteAsync(
            updatedBy, "price_updated", "market_product", marketProductId,
            Arg.Any<string>(), OccurredAt, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OrderConfirmedHandler_WritesMappedAuditLogAsync()
    {
        var orderId = Guid.NewGuid();
        var restaurantId = Guid.NewGuid();
        const decimal totalAmount = 120_000m;
        var sut = new OrderConfirmedAuditLogHandler(_writer);

        await sut.Handle(
            new OrderConfirmedIntegrationEvent(orderId, restaurantId, totalAmount, OccurredAt),
            default);

        await _writer.Received(1).WriteAsync(
            null,
            "order_confirmed",
            "order",
            orderId,
            JsonSerializer.Serialize(new { restaurantId, totalAmount }),
            OccurredAt,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcurementBatchBuiltHandler_WritesMappedAuditLogUsingTimeProviderAsync()
    {
        var batchId = Guid.NewGuid();
        var marketId = Guid.NewGuid();
        var batchDate = new DateOnly(2026, 7, 16);
        var sut = new ProcurementBatchBuiltAuditLogHandler(
            _writer,
            new FakeTimeProvider(new DateTimeOffset(OccurredAt)));

        await sut.Handle(
            new ProcurementBatchBuiltIntegrationEvent(
                batchId,
                marketId,
                batchDate,
                [Guid.NewGuid(), Guid.NewGuid()]),
            default);

        await _writer.Received(1).WriteAsync(
            null,
            "procurement_batch_built",
            "procurement_batch",
            batchId,
            JsonSerializer.Serialize(new { marketId, batchDate, coveredOrderCount = 2 }),
            OccurredAt,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcurementManifestGeneratedHandler_WritesMappedAuditLogAsync()
    {
        var batchId = Guid.NewGuid();
        var marketId = Guid.NewGuid();
        var batchDate = new DateOnly(2026, 7, 16);
        var sut = new ProcurementManifestGeneratedAuditLogHandler(_writer);

        await sut.Handle(
            new ProcurementManifestGeneratedIntegrationEvent(batchId, marketId, batchDate, OccurredAt),
            default);

        await _writer.Received(1).WriteAsync(
            null,
            "procurement_manifest_generated",
            "procurement_batch",
            batchId,
            JsonSerializer.Serialize(new { marketId, batchDate }),
            OccurredAt,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcurementAgentAssignedHandler_WritesMappedAuditLogAsync()
    {
        var batchId = Guid.NewGuid();
        var marketId = Guid.NewGuid();
        var agentUserId = Guid.NewGuid();
        var sut = new ProcurementAgentAssignedAuditLogHandler(_writer);

        await sut.Handle(
            new ProcurementAgentAssignedIntegrationEvent(batchId, marketId, agentUserId, OccurredAt),
            default);

        await _writer.Received(1).WriteAsync(
            null,
            "procurement_agent_assigned",
            "procurement_batch",
            batchId,
            JsonSerializer.Serialize(new { marketId, agentUserId }),
            OccurredAt,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcurementBatchHandedOffHandler_WritesMappedAuditLogAsync()
    {
        var batchId = Guid.NewGuid();
        var marketId = Guid.NewGuid();
        Guid? hubId = Guid.NewGuid();
        var sut = new ProcurementBatchHandedOffAuditLogHandler(_writer);

        await sut.Handle(
            new ProcurementBatchHandedOffIntegrationEvent(
                batchId,
                marketId,
                hubId,
                OccurredAt,
                [Guid.NewGuid(), Guid.NewGuid()]),
            default);

        await _writer.Received(1).WriteAsync(
            null,
            "procurement_batch_handed_off",
            "procurement_batch",
            batchId,
            JsonSerializer.Serialize(new { marketId, hubId, coveredOrderCount = 2 }),
            OccurredAt,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeliveryStartedHandler_WritesMappedAuditLogAsync()
    {
        var routeId = Guid.NewGuid();
        var sut = new DeliveryStartedAuditLogHandler(_writer);

        await sut.Handle(
            new DeliveryStartedIntegrationEvent(routeId, [Guid.NewGuid(), Guid.NewGuid()], OccurredAt),
            default);

        await _writer.Received(1).WriteAsync(
            null,
            "delivery_started",
            "delivery_route",
            routeId,
            JsonSerializer.Serialize(new { orderCount = 2 }),
            OccurredAt,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeliveryCompletedHandler_WritesMappedAuditLogAsync()
    {
        var orderId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var actualArrivalAt = OccurredAt.AddMinutes(-5);
        var sut = new DeliveryCompletedAuditLogHandler(_writer);

        await sut.Handle(
            new DeliveryCompletedIntegrationEvent(orderId, routeId, actualArrivalAt, OccurredAt),
            default);

        await _writer.Received(1).WriteAsync(
            null,
            "delivery_completed",
            "order",
            orderId,
            JsonSerializer.Serialize(new { routeId, actualArrivalAt }),
            OccurredAt,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HubDiscrepancyRecordedHandler_WritesMappedAuditLogAsync()
    {
        var discrepancyId = Guid.NewGuid();
        var hubId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        const decimal affectedQuantity = 2.5m;
        const string conditionStatus = "DAMAGED";
        var sut = new HubDiscrepancyRecordedAuditLogHandler(_writer);

        await sut.Handle(
            new HubDiscrepancyRecordedIntegrationEvent(
                discrepancyId,
                hubId,
                Guid.NewGuid(),
                orderId,
                Guid.NewGuid(),
                affectedQuantity,
                conditionStatus,
                OccurredAt),
            default);

        await _writer.Received(1).WriteAsync(
            null,
            "hub_discrepancy_recorded",
            "hub_discrepancy",
            discrepancyId,
            JsonSerializer.Serialize(new { hubId, orderId, affectedQuantity, conditionStatus }),
            OccurredAt,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewHandlers_WhenAuditPersistenceFails_DoNotPropagateAsync()
    {
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(_ => throw new InvalidOperationException("Database unavailable"));
        IAuditLogWriter writer = new AuditLogWriter(
            scopeFactory,
            Substitute.For<ILogger<AuditLogWriter>>());
        var clock = new FakeTimeProvider(new DateTimeOffset(OccurredAt));

        Func<Task>[] writes =
        [
            () => new OrderConfirmedAuditLogHandler(writer).Handle(
                new(Guid.NewGuid(), Guid.NewGuid(), 1m, OccurredAt), default),
            () => new ProcurementBatchBuiltAuditLogHandler(writer, clock).Handle(
                new(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 7, 16), []), default),
            () => new ProcurementManifestGeneratedAuditLogHandler(writer).Handle(
                new(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 7, 16), OccurredAt), default),
            () => new ProcurementAgentAssignedAuditLogHandler(writer).Handle(
                new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), OccurredAt), default),
            () => new ProcurementBatchHandedOffAuditLogHandler(writer).Handle(
                new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), OccurredAt, []), default),
            () => new DeliveryStartedAuditLogHandler(writer).Handle(
                new(Guid.NewGuid(), [], OccurredAt), default),
            () => new DeliveryCompletedAuditLogHandler(writer).Handle(
                new(Guid.NewGuid(), Guid.NewGuid(), OccurredAt, OccurredAt), default),
            () => new HubDiscrepancyRecordedAuditLogHandler(writer).Handle(
                new(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    1m,
                    "DAMAGED",
                    OccurredAt),
                default),
        ];

        foreach (var write in writes)
            await write.Should().NotThrowAsync();
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}

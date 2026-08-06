using FluentAssertions;
using FreshFlow.Contracts;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.EventHandlers;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FreshFlow.Procurement.UnitTests.EventHandlers;

[Trait("Category", "Unit")]
public sealed class DeliveryCompletedIntegrationEventHandlerTests
{
    [Fact]
    public async Task Handle_AllCoveredOrdersSettled_CompletesBatchAndSavesAsync()
    {
        var firstOrderId = Guid.NewGuid();
        var secondOrderId = Guid.NewGuid();
        var batch = BuildHandedOffBatch(firstOrderId, secondOrderId);
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.FindByOrderIdAsync(secondOrderId, default).Returns(batch);
        repository.SaveChangesAsync(default).Returns(true);
        var orders = Substitute.For<IConfirmedOrderReader>();
        orders.ReadStatusesAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids =>
                    ids.OrderBy(id => id).SequenceEqual(new[] { firstOrderId, secondOrderId }.OrderBy(id => id))),
                default)
            .Returns(new Dictionary<Guid, string>
            {
                [firstOrderId] = "Delivered",
                [secondOrderId] = "Delivered"
            });
        var handler = new DeliveryCompletedIntegrationEventHandler(
            repository,
            orders,
            Substitute.For<ILogger<DeliveryCompletedIntegrationEventHandler>>());
        var occurredAt = new DateTime(2026, 8, 6, 10, 0, 0, DateTimeKind.Utc);

        await handler.Handle(
            new DeliveryCompletedIntegrationEvent(secondOrderId, Guid.NewGuid(), occurredAt, occurredAt),
            default);

        batch.Status.Should().Be(ProcurementBatchStatus.Completed);
        batch.CompletedAt.Should().Be(occurredAt);
        await repository.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_OneOrderStillOpen_DoesNotCompleteOrSaveAsync()
    {
        var firstOrderId = Guid.NewGuid();
        var secondOrderId = Guid.NewGuid();
        var batch = BuildHandedOffBatch(firstOrderId, secondOrderId);
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.FindByOrderIdAsync(firstOrderId, default).Returns(batch);
        var orders = Substitute.For<IConfirmedOrderReader>();
        orders.ReadStatusesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), default)
            .Returns(new Dictionary<Guid, string>
            {
                [firstOrderId] = "Delivered",
                [secondOrderId] = "Delivering"
            });
        var handler = new DeliveryCompletedIntegrationEventHandler(
            repository,
            orders,
            Substitute.For<ILogger<DeliveryCompletedIntegrationEventHandler>>());

        await handler.Handle(
            new DeliveryCompletedIntegrationEvent(firstOrderId, Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow),
            default);

        batch.Status.Should().Be(ProcurementBatchStatus.HandedOff);
        await repository.DidNotReceive().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_OrderNotInAnyBatch_NoOpsAsync()
    {
        var orderId = Guid.NewGuid();
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.FindByOrderIdAsync(orderId, default).Returns((ProcurementBatch?)null);
        var orders = Substitute.For<IConfirmedOrderReader>();
        var handler = new DeliveryCompletedIntegrationEventHandler(
            repository,
            orders,
            Substitute.For<ILogger<DeliveryCompletedIntegrationEventHandler>>());

        await handler.Handle(
            new DeliveryCompletedIntegrationEvent(orderId, Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow),
            default);

        await orders.DidNotReceive().ReadStatusesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), default);
        await repository.DidNotReceive().SaveChangesAsync(default);
    }

    [Theory]
    [InlineData(ProcurementBatchStatus.Purchasing)]
    [InlineData(ProcurementBatchStatus.Completed)]
    public async Task Handle_BatchNotHandedOff_NoOpsAsync(ProcurementBatchStatus status)
    {
        var orderId = Guid.NewGuid();
        var batch = BuildHandedOffBatch(orderId);
        typeof(ProcurementBatch).GetProperty(nameof(ProcurementBatch.Status))!
            .SetValue(batch, status);
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.FindByOrderIdAsync(orderId, default).Returns(batch);
        var orders = Substitute.For<IConfirmedOrderReader>();
        var handler = new DeliveryCompletedIntegrationEventHandler(
            repository,
            orders,
            Substitute.For<ILogger<DeliveryCompletedIntegrationEventHandler>>());

        await handler.Handle(
            new DeliveryCompletedIntegrationEvent(orderId, Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow),
            default);

        await orders.DidNotReceive().ReadStatusesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), default);
        await repository.DidNotReceive().SaveChangesAsync(default);
        batch.Status.Should().Be(status);
    }

    private static ProcurementBatch BuildHandedOffBatch(params Guid[] orderIds)
    {
        var marketId = Guid.NewGuid();
        var hubId = Guid.NewGuid();
        var batch = ProcurementBatch.Build(
            new DateOnly(2026, 8, 5),
            marketId,
            orderIds.Select(orderId => (Guid.NewGuid(), "Product", 2, orderId)),
            hubId).Value;
        batch.Manifest(
            batch.Items.ToDictionary(item => item.MarketProductId, _ => 10_000m),
            new DateTime(2026, 8, 5, 1, 0, 0, DateTimeKind.Utc));
        batch.ConfirmPurchase(
            batch.Items.ToDictionary(
                item => item.MarketProductId,
                _ => (ActualQuantity: 2, ActualUnitPrice: 10_000m)),
            new DateTime(2026, 8, 5, 2, 0, 0, DateTimeKind.Utc));
        batch.HandoverToHub(new DateTime(2026, 8, 5, 3, 0, 0, DateTimeKind.Utc));
        batch.ClearDomainEvents();
        return batch;
    }
}

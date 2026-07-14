using FreshFlow.Contracts;
using FreshFlow.Procurement.Application.EventHandlers;
using FreshFlow.Procurement.Domain.Events;
using MediatR;
using NSubstitute;

namespace FreshFlow.Procurement.UnitTests.EventHandlers;

[Trait("Category", "Unit")]
public sealed class ProcurementBatchHandedOffDomainEventHandlerTests
{
    [Fact]
    public async Task Handle_PublishesBatchHandedOffIntegrationEventAsync()
    {
        var publisher = Substitute.For<IPublisher>();
        var domainEvent = new ProcurementBatchHandedOffDomainEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateTime(2026, 7, 15, 4, 0, 0, DateTimeKind.Utc),
            [Guid.NewGuid(), Guid.NewGuid()]);

        await new ProcurementBatchHandedOffDomainEventHandler(publisher)
            .Handle(domainEvent, default);

        await publisher.Received(1).Publish(
            Arg.Is<ProcurementBatchHandedOffIntegrationEvent>(integrationEvent =>
                integrationEvent.BatchId == domainEvent.BatchId &&
                integrationEvent.MarketId == domainEvent.MarketId &&
                integrationEvent.HubId == domainEvent.HubId &&
                integrationEvent.HandedOffAt == domainEvent.HandedOffAt &&
                integrationEvent.CoveredOrderIds.SequenceEqual(domainEvent.CoveredOrderIds)),
            default);
    }
}

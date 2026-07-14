using FreshFlow.Contracts;
using FreshFlow.Procurement.Application.EventHandlers;
using FreshFlow.Procurement.Domain.Events;
using MediatR;
using NSubstitute;

namespace FreshFlow.Procurement.UnitTests.EventHandlers;

[Trait("Category", "Unit")]
public sealed class ProcurementBatchBuiltDomainEventHandlerTests
{
    [Fact]
    public async Task Handle_PublishesCoveredOrderIdsAsync()
    {
        var publisher = Substitute.For<IPublisher>();
        var orderIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var domainEvent = new ProcurementBatchBuiltDomainEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 7, 15),
            orderIds);

        await new ProcurementBatchBuiltDomainEventHandler(publisher)
            .Handle(domainEvent, default);

        await publisher.Received(1).Publish(
            Arg.Is<ProcurementBatchBuiltIntegrationEvent>(integrationEvent =>
                integrationEvent.BatchId == domainEvent.BatchId &&
                integrationEvent.CoveredOrderIds.SequenceEqual(orderIds)),
            default);
    }
}

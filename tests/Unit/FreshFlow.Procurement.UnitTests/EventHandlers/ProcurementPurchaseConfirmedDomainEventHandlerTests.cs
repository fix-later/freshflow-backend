using FreshFlow.Contracts;
using FreshFlow.Procurement.Application.EventHandlers;
using FreshFlow.Procurement.Domain.Events;
using MediatR;
using NSubstitute;

namespace FreshFlow.Procurement.UnitTests.EventHandlers;

[Trait("Category", "Unit")]
public sealed class ProcurementPurchaseConfirmedDomainEventHandlerTests
{
    [Fact]
    public async Task Handle_PublishesActualPricesAsync()
    {
        var publisher = Substitute.For<IPublisher>();
        var prices = new Dictionary<Guid, decimal> { [Guid.NewGuid()] = 12_000m };
        var domainEvent = new ProcurementPurchaseConfirmedDomainEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            prices,
            DateTime.UtcNow);

        await new ProcurementPurchaseConfirmedDomainEventHandler(publisher)
            .Handle(domainEvent, default);

        await publisher.Received(1).Publish(
            Arg.Is<ProcurementPurchaseConfirmedIntegrationEvent>(integrationEvent =>
                integrationEvent.BatchId == domainEvent.BatchId &&
                integrationEvent.MarketId == domainEvent.MarketId &&
                integrationEvent.AgentUserId == domainEvent.AgentUserId &&
                integrationEvent.ActualUnitPrices == prices),
            default);
    }
}

using FreshFlow.Contracts;
using FreshFlow.Procurement.Application.EventHandlers;
using FreshFlow.Procurement.Domain.Events;
using MediatR;
using NSubstitute;

namespace FreshFlow.Procurement.UnitTests.EventHandlers;

[Trait("Category", "Unit")]
public sealed class ProcurementAgentAssignedDomainEventHandlerTests
{
    [Fact]
    public async Task Handle_PublishesAgentAssignedIntegrationEventAsync()
    {
        var publisher = Substitute.For<IPublisher>();
        var domainEvent = new ProcurementAgentAssignedDomainEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateTime(2026, 7, 15, 2, 0, 0, DateTimeKind.Utc));

        await new ProcurementAgentAssignedDomainEventHandler(publisher)
            .Handle(domainEvent, default);

        await publisher.Received(1).Publish(
            Arg.Is<ProcurementAgentAssignedIntegrationEvent>(integrationEvent =>
                integrationEvent.BatchId == domainEvent.BatchId &&
                integrationEvent.MarketId == domainEvent.MarketId &&
                integrationEvent.AgentUserId == domainEvent.AgentUserId &&
                integrationEvent.AssignedAt == domainEvent.AssignedAt),
            default);
    }
}

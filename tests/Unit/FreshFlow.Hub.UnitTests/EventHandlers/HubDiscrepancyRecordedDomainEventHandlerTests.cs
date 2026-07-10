using FluentAssertions;
using FreshFlow.Contracts;
using FreshFlow.Hub.Application.EventHandlers;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.Hub.Domain.Events;
using MediatR;
using NSubstitute;

namespace FreshFlow.Hub.UnitTests.EventHandlers;

[Trait("Category", "Unit")]
public sealed class HubDiscrepancyRecordedDomainEventHandlerTests
{
    [Fact]
    public async Task Handle_PublishesHubDiscrepancyRecordedIntegrationEventAsync()
    {
        var publisher = Substitute.For<IPublisher>();
        var sut = new HubDiscrepancyRecordedDomainEventHandler(publisher);
        var domainEvent = new HubDiscrepancyRecordedDomainEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            2m,
            HubDiscrepancy.ConditionDamaged,
            DateTime.UtcNow);

        await sut.Handle(domainEvent, default);

        await publisher.Received(1).Publish(
            Arg.Is<HubDiscrepancyRecordedIntegrationEvent>(evt =>
                evt.DiscrepancyId == domainEvent.DiscrepancyId &&
                evt.HubId == domainEvent.HubId &&
                evt.InboundEventId == domainEvent.InboundEventId &&
                evt.OrderId == domainEvent.OrderId &&
                evt.OrderItemId == domainEvent.OrderItemId &&
                evt.AffectedQuantity == domainEvent.AffectedQuantity &&
                evt.ConditionStatus == domainEvent.ConditionStatus &&
                evt.OccurredAt == domainEvent.OccurredAt),
            default);
    }
}

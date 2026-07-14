using FreshFlow.Contracts;
using FreshFlow.Procurement.Application.EventHandlers;
using FreshFlow.Procurement.Domain.Events;
using MediatR;
using NSubstitute;

namespace FreshFlow.Procurement.UnitTests.EventHandlers;

[Trait("Category", "Unit")]
public sealed class ProcurementManifestGeneratedDomainEventHandlerTests
{
    [Fact]
    public async Task Handle_PublishesManifestIntegrationEventAsync()
    {
        var publisher = Substitute.For<IPublisher>();
        var domainEvent = new ProcurementManifestGeneratedDomainEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 7, 15),
            new DateTime(2026, 7, 14, 16, 0, 0, DateTimeKind.Utc));

        await new ProcurementManifestGeneratedDomainEventHandler(publisher)
            .Handle(domainEvent, default);

        await publisher.Received(1).Publish(
            Arg.Is<ProcurementManifestGeneratedIntegrationEvent>(integrationEvent =>
                integrationEvent.BatchId == domainEvent.BatchId &&
                integrationEvent.MarketId == domainEvent.MarketId &&
                integrationEvent.BatchDate == domainEvent.BatchDate &&
                integrationEvent.ManifestedAt == domainEvent.ManifestedAt),
            default);
    }
}

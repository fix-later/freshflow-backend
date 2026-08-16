using FreshFlow.Contracts;
using FreshFlow.Procurement.Domain.Events;
using MediatR;

namespace FreshFlow.Procurement.Application.EventHandlers;

internal sealed class ProcurementAgentAssignedDomainEventHandler(IPublisher publisher)
    : INotificationHandler<ProcurementAgentAssignedDomainEvent>
{
    public Task Handle(
        ProcurementAgentAssignedDomainEvent notification,
        CancellationToken cancellationToken) =>
        publisher.Publish(
            new ProcurementAgentAssignedIntegrationEvent(
                notification.BatchId,
                notification.MarketId,
                notification.AgentUserId,
                notification.AssignedAt),
            cancellationToken);
}

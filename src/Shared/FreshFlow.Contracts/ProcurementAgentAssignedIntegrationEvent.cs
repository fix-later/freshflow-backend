using MediatR;

namespace FreshFlow.Contracts;

public sealed record ProcurementAgentAssignedIntegrationEvent(
    Guid BatchId,
    Guid MarketId,
    Guid AgentUserId,
    DateTime AssignedAt) : INotification;

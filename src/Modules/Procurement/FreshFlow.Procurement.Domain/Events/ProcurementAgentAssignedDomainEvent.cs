using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Procurement.Domain.Events;

public sealed record ProcurementAgentAssignedDomainEvent(
    Guid BatchId,
    Guid MarketId,
    Guid AgentUserId,
    DateTime AssignedAt) : IDomainEvent;

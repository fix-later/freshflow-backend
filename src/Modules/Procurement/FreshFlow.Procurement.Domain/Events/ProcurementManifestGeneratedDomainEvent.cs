using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Procurement.Domain.Events;

public sealed record ProcurementManifestGeneratedDomainEvent(
    Guid BatchId,
    Guid MarketId,
    DateOnly BatchDate,
    DateTime ManifestedAt) : IDomainEvent;

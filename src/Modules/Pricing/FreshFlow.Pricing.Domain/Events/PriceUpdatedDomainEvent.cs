using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Pricing.Domain.Events;

public sealed record PriceUpdatedDomainEvent(
    Guid MarketProductId,
    decimal OldPrice,
    decimal NewPrice,
    Guid? UpdatedBy,
    DateTime OccurredAt) : IDomainEvent;

using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Pricing.Domain.Events;

/// <summary>
/// Raised by <see cref="Entities.MarketProduct.ApplyUpdate"/> whenever a market product
/// price and/or quantity is updated. Carries enough context for downstream handlers
/// (price history, Redis cache, SignalR broadcast) without requiring a DB round-trip.
/// </summary>
public sealed record PriceUpdatedDomainEvent(
    Guid MarketProductId,
    Guid MarketId,
    Guid ProductId,
    decimal OldPrice,
    decimal NewPrice,
    int CurrentQuantity,
    Guid? UpdatedBy,
    DateTime OccurredAt) : IDomainEvent;

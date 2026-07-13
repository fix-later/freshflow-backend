using MediatR;

namespace FreshFlow.Contracts;

/// <summary>
/// A market product's price and/or quantity was updated (SCRUM-359a). Published for
/// cross-module consumption (Admin audit log).
/// </summary>
public sealed record PriceUpdatedIntegrationEvent(
    Guid MarketProductId,
    Guid MarketId,
    Guid ProductId,
    decimal OldPrice,
    decimal NewPrice,
    int CurrentQuantity,
    Guid? UpdatedBy,
    DateTime OccurredAt) : INotification;

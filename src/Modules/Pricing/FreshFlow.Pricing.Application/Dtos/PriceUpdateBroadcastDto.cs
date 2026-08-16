namespace FreshFlow.Pricing.Application.Dtos;

/// <summary>
/// Payload broadcast to SignalR clients on every committed price or quantity update.
/// Mirrors <c>PriceUpdatedDomainEvent</c> fields so clients can update their UI
/// without a follow-up REST call.
/// </summary>
public sealed record PriceUpdateBroadcastDto(
    Guid MarketProductId,
    Guid MarketId,
    Guid ProductId,
    decimal OldPrice,
    decimal NewPrice,
    int CurrentQuantity,
    Guid? UpdatedBy,
    DateTime OccurredAt);

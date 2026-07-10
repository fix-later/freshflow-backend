namespace FreshFlow.Hub.Domain.Entities;

public sealed record HubOutboundItem(
    Guid MarketProductId,
    Guid? ProductId,
    decimal QuantityKg);

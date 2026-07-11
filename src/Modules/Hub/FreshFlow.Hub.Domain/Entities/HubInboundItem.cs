namespace FreshFlow.Hub.Domain.Entities;

public sealed record HubInboundItem(
    Guid MarketProductId,
    Guid? ProductId,
    decimal QuantityKg);

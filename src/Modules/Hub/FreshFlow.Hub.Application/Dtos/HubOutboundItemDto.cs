namespace FreshFlow.Hub.Application.Dtos;

public sealed record HubOutboundItemDto(
    Guid MarketProductId,
    Guid? ProductId,
    decimal QuantityKg);

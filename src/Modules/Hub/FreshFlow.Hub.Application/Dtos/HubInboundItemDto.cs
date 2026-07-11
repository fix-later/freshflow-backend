namespace FreshFlow.Hub.Application.Dtos;

public sealed record HubInboundItemDto(
    Guid MarketProductId,
    Guid? ProductId,
    decimal QuantityKg);

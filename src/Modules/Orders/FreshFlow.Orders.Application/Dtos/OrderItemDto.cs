namespace FreshFlow.Orders.Application.Dtos;

public sealed record OrderItemDto(
    Guid OrderItemId,
    Guid MarketProductId,
    string ProductNameSnapshot,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal,
    decimal? ActualQuantity);

namespace FreshFlow.Orders.Application.Dtos;

public sealed record OrderItemDto(
    Guid OrderItemId,
    Guid MarketProductId,
    string ProductNameSnapshot,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal,
    decimal? ActualQuantity,
    decimal? ActualUnitPrice,
    string? ImageUrl,
    string? VatRateCode = null,
    decimal? VatRatePercent = null,
    decimal? VatAmount = null,
    string? PackingCode = null);

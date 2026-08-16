namespace FreshFlow.Logistics.Application.Dtos;

public sealed record OrderPackingLine(
    Guid OrderId,
    Guid OrderItemId,
    string ProductName,
    decimal Quantity,
    decimal? CapacityKg,
    Guid MarketProductId);

public sealed record OrderPackingLines(
    Guid OrderId,
    IReadOnlyList<OrderPackingLine> Lines);

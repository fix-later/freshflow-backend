namespace FreshFlow.Logistics.Application.Dtos;

public sealed record OrderPackingLine(
    string ProductName,
    int Quantity,
    decimal? CapacityKg);

public sealed record OrderPackingLines(
    Guid OrderId,
    IReadOnlyList<OrderPackingLine> Lines);

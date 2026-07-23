namespace FreshFlow.Logistics.Application.Dtos;

public sealed record OrderPackingLine(
    string ProductName,
    int Quantity,
    decimal? CapacityKg);

namespace FreshFlow.Logistics.Application.Dtos;

public sealed record ShipmentLineDto(
    string ProductName,
    decimal Quantity,
    decimal CapacityKg,
    int Boxes,
    decimal LoadKg);

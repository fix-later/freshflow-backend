namespace FreshFlow.Logistics.Application.Dtos;

public sealed record ShipmentLineDto(
    string ProductName,
    int Quantity,
    decimal CapacityKg,
    int Boxes,
    decimal LoadKg);

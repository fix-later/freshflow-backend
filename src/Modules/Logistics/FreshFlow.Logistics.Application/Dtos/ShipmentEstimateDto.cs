namespace FreshFlow.Logistics.Application.Dtos;

public sealed record ShipmentEstimateDto(
    Guid OrderId,
    int TotalBoxes,
    decimal TotalLoadKg,
    decimal BoxTareKg,
    Guid? VehicleId,
    decimal? VehicleCapacityKg,
    bool? FitsVehicle,
    IReadOnlyList<ShipmentLineDto> Lines,
    IReadOnlyList<string> MissingPackingCode);

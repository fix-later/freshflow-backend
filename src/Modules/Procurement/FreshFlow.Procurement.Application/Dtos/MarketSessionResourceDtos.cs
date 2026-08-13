namespace FreshFlow.Procurement.Application.Dtos;

public sealed record MarketSessionResourcesDto(
    Guid SessionId,
    decimal? PlannedCapacityKg,
    decimal ReferenceVehicleCapacityKg,
    decimal SelectedVehicleCapacityKg,
    IReadOnlyList<MarketSessionResourceVehicleDto> Vehicles,
    IReadOnlyList<MarketSessionResourceAgentDto> Agents);

public sealed record MarketSessionResourceVehicleDto(
    Guid VehicleId,
    string PlateNumber,
    decimal CapacityKg,
    string VehicleType,
    bool Selected);

public sealed record MarketSessionResourceAgentDto(
    Guid UserId,
    string Email,
    string? FullName,
    bool Selected);

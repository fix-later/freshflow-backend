namespace FreshFlow.Hub.Application.Dtos;

public sealed record HubDto(
    Guid HubId,
    Guid? MarketId,
    string Name,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    decimal CapacityKg,
    decimal OccupiedCapacityKg,
    decimal AvailableCapacityKg,
    bool IsActive,
    Guid? ManagedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt);

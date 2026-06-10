namespace FreshFlow.Catalog.Application.Dtos;

public sealed record MarketDto(
    Guid Id,
    string Name,
    string? Location,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

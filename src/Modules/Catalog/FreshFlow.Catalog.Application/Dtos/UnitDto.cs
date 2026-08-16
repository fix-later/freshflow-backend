namespace FreshFlow.Catalog.Application.Dtos;

public record UnitDto(
    Guid Id,
    string Name,
    string? Abbreviation,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

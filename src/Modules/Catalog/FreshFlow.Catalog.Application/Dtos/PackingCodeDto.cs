namespace FreshFlow.Catalog.Application.Dtos;

public sealed record PackingCodeDto(
    Guid Id,
    string Code,
    string? Description,
    decimal CapacityKg,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

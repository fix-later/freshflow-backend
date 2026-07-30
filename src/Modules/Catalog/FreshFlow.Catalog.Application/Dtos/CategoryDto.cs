namespace FreshFlow.Catalog.Application.Dtos;

public sealed record CategoryDto(
    Guid Id,
    string Name,
    Guid? ParentId,
    string? ImageUrl,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

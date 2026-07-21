namespace FreshFlow.Catalog.Application.Dtos;

public sealed record CategoryDto(
    Guid Id,
    string Name,
    Guid? ParentId,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

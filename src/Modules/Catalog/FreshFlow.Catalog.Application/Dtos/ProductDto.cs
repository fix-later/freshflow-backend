namespace FreshFlow.Catalog.Application.Dtos;

public record ProductDto(
    Guid Id,
    string Name,
    Guid? CategoryId,
    string? CategoryName,
    Guid UnitId,
    string? UnitName,
    Guid? PackingCodeId,
    string? Description,
    Guid? CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsDeleted,
    string? ImageUrl = null);

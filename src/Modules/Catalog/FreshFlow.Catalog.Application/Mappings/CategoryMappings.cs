using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.Catalog.Domain.Entities;

namespace FreshFlow.Catalog.Application.Mappings;

internal static class CategoryMappings
{
    internal static CategoryDto ToDto(this ProductCategory c) =>
        new(c.Id, c.Name, c.ParentId, c.ImageUrl, c.IsActive, c.CreatedAt, c.UpdatedAt);
}

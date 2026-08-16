using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.Catalog.Domain.Entities;

namespace FreshFlow.Catalog.Application.Mappings;

internal static class UnitMappings
{
    internal static UnitDto ToDto(this UnitOfMeasurement u) =>
        new(u.Id, u.Name, u.Abbreviation, u.IsActive, u.CreatedAt, u.UpdatedAt);
}

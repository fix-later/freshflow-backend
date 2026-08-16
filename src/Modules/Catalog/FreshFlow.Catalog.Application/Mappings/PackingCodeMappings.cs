using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.Catalog.Domain.Entities;

namespace FreshFlow.Catalog.Application.Mappings;

internal static class PackingCodeMappings
{
    internal static PackingCodeDto ToDto(this PackingCode code) =>
        new(code.Id, code.Code, code.Description, code.CapacityKg,
            code.IsActive, code.CreatedAt, code.UpdatedAt);
}

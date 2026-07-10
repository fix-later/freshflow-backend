namespace FreshFlow.Logistics.Application.Dtos;

public sealed record RoutePageDto(IReadOnlyList<RouteDto> Items, int PageSize, string? NextCursor);

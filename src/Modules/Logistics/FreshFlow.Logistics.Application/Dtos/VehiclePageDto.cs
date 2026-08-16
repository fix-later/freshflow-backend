namespace FreshFlow.Logistics.Application.Dtos;

public sealed record VehiclePageDto(
    IReadOnlyList<VehicleDto> Items,
    int PageSize,
    string? NextCursor);

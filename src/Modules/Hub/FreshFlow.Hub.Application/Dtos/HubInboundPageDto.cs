namespace FreshFlow.Hub.Application.Dtos;

public sealed record HubInboundPageDto(
    IReadOnlyList<HubInboundDto> Items,
    int PageSize,
    string? NextCursor,
    decimal TotalQuantityKg);

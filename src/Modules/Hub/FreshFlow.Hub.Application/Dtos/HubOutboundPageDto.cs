namespace FreshFlow.Hub.Application.Dtos;

public sealed record HubOutboundPageDto(
    IReadOnlyList<HubOutboundEventDto> Items,
    int PageSize,
    string? NextCursor,
    decimal TotalQuantityKg);

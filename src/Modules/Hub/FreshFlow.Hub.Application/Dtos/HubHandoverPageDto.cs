namespace FreshFlow.Hub.Application.Dtos;

public sealed record HubHandoverPageDto(
    IReadOnlyList<HubHandoverDto> Items,
    int PageSize,
    string? NextCursor);

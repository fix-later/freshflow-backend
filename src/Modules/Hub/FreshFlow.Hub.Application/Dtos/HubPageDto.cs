namespace FreshFlow.Hub.Application.Dtos;

public sealed record HubPageDto(
    IReadOnlyList<HubDto> Items,
    int PageSize,
    string? NextCursor);

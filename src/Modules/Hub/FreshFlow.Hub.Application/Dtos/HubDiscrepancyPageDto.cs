namespace FreshFlow.Hub.Application.Dtos;

public sealed record HubDiscrepancyPageDto(
    IReadOnlyList<HubDiscrepancyDto> Items,
    int PageSize,
    string? NextCursor);

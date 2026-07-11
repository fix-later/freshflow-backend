namespace FreshFlow.Hub.Application.Dtos;

public sealed record CrossDockTransferPageDto(
    IReadOnlyList<CrossDockTransferDto> Items,
    int PageSize,
    string? NextCursor);

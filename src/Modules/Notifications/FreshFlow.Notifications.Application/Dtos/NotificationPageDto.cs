namespace FreshFlow.Notifications.Application.Dtos;

public sealed record NotificationPageDto(
    IReadOnlyList<NotificationDto> Items,
    int PageSize,
    string? NextCursor);

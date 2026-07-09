namespace FreshFlow.Notifications.Application.Dtos;

public sealed record NotificationDto(
    Guid Id,
    string Type,
    string Title,
    string Body,
    string? Payload,
    bool IsRead,
    DateTime? ReadAt,
    DateTime CreatedAt);

namespace FreshFlow.Notifications.Application.Dtos;

public sealed record NotificationDeviceDto(
    Guid Id,
    Guid UserId,
    string Token,
    string Platform,
    string? DeviceId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? RevokedAt);

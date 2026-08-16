using FreshFlow.Notifications.Application.Dtos;
using FreshFlow.Notifications.Domain.Entities;

namespace FreshFlow.Notifications.Application.Mappers;

internal static class NotificationDtoMapper
{
    public static NotificationDto ToDto(this Notification notification) =>
        new(
            notification.Id,
            notification.Type.ToString(),
            notification.Title,
            notification.Body,
            notification.Payload,
            notification.IsRead,
            notification.ReadAt,
            notification.CreatedAt);
}

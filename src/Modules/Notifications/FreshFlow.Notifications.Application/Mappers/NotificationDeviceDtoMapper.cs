using FreshFlow.Notifications.Application.Dtos;
using FreshFlow.Notifications.Domain.Entities;
using FreshFlow.Notifications.Domain.Enums;

namespace FreshFlow.Notifications.Application.Mappers;

internal static class NotificationDeviceDtoMapper
{
    public static NotificationDeviceDto ToDto(this NotificationDevice device) =>
        new(
            device.Id,
            device.UserId,
            device.Token,
            device.Platform.ToApiValue(),
            device.DeviceId,
            device.CreatedAt,
            device.UpdatedAt,
            device.RevokedAt);
}

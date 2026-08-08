using FreshFlow.Notifications.Domain.Entities;
using FreshFlow.Notifications.Domain.Enums;

namespace FreshFlow.Notifications.Application.Abstractions;

public interface INotificationDeviceRepository
{
    public Task<NotificationDevice> RegisterAsync(
        Guid userId,
        string token,
        NotificationDevicePlatform platform,
        string? deviceId,
        CancellationToken ct);

    public Task<NotificationDevice?> UnregisterAsync(Guid userId, string token, CancellationToken ct);

    public Task<IReadOnlyList<NotificationDevice>> GetActiveMobileAsync(
        Guid userId,
        CancellationToken ct);

    public Task RevokeTokenAsync(string token, CancellationToken ct);
}

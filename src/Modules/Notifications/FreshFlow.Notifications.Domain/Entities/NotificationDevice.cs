using FreshFlow.Notifications.Domain.Enums;

namespace FreshFlow.Notifications.Domain.Entities;

public sealed class NotificationDevice
{
    private NotificationDevice() { } // EF Core

    public NotificationDevice(
        Guid userId,
        string token,
        NotificationDevicePlatform platform,
        string? deviceId)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User id is required.", nameof(userId));

        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token is required.", nameof(token));

        Id = Guid.NewGuid();
        UserId = userId;
        Token = token.Trim();
        Platform = platform;
        DeviceId = NormalizeOptional(deviceId);
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public NotificationDevicePlatform Platform { get; private set; }
    public string? DeviceId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public bool IsActive => RevokedAt is null;

    public void Reactivate(NotificationDevicePlatform platform, string? deviceId)
    {
        Platform = platform;
        DeviceId = NormalizeOptional(deviceId);
        RevokedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Revoke()
    {
        if (RevokedAt is not null)
            return;

        RevokedAt = DateTime.UtcNow;
        UpdatedAt = RevokedAt.Value;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Domain.Entities;
using FreshFlow.Notifications.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreshFlow.Notifications.Infrastructure.Repositories;

internal sealed class NotificationDeviceRepository(AppDbContext db) : INotificationDeviceRepository
{
    public async Task<NotificationDevice> RegisterAsync(
        Guid userId,
        string token,
        NotificationDevicePlatform platform,
        string? deviceId,
        CancellationToken ct)
    {
        var normalizedToken = token.Trim();
        var normalizedDeviceId = string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim();

        try
        {
            return await RegisterCoreAsync(
                userId, normalizedToken, platform, normalizedDeviceId, ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            db.ChangeTracker.Clear();
            return await RegisterCoreAsync(
                userId, normalizedToken, platform, normalizedDeviceId, ct);
        }
    }

    public async Task<NotificationDevice?> UnregisterAsync(Guid userId, string token, CancellationToken ct)
    {
        var normalizedToken = token.Trim();
        var device = await db.Set<NotificationDevice>()
            .FirstOrDefaultAsync(d => d.UserId == userId && d.Token == normalizedToken, ct);

        if (device is null)
            return null;

        device.Revoke();
        await db.SaveChangesAsync(ct);
        return device;
    }

    public async Task<IReadOnlyList<NotificationDevice>> GetActiveMobileAsync(
        Guid userId,
        CancellationToken ct) =>
        await db.Set<NotificationDevice>()
            .AsNoTracking()
            .Where(d => d.UserId == userId
                && d.RevokedAt == null
                && (d.Platform == NotificationDevicePlatform.ios
                    || d.Platform == NotificationDevicePlatform.android))
            .ToListAsync(ct);

    public async Task RevokeTokenAsync(string token, CancellationToken ct)
    {
        var normalizedToken = token.Trim();
        var device = await db.Set<NotificationDevice>()
            .FirstOrDefaultAsync(d => d.Token == normalizedToken && d.RevokedAt == null, ct);

        if (device is null)
            return;

        device.Revoke();
        await db.SaveChangesAsync(ct);
    }

    internal static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation;

    private async Task<NotificationDevice> RegisterCoreAsync(
        Guid userId,
        string token,
        NotificationDevicePlatform platform,
        string? deviceId,
        CancellationToken ct)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;
        var activeMatches = await db.Set<NotificationDevice>()
            .Where(d => d.RevokedAt == null &&
                (d.Token == token || (deviceId != null && d.DeviceId == deviceId)))
            .ToListAsync(ct);

        var device = activeMatches.FirstOrDefault(d => d.Token == token)
            ?? activeMatches.FirstOrDefault();

        foreach (var conflict in activeMatches.Where(d => d != device))
            conflict.Revoke();

        if (activeMatches.Count > 1)
            await db.SaveChangesAsync(ct);

        if (device is null)
        {
            device = await db.Set<NotificationDevice>()
                .Where(d => d.UserId == userId && d.Token == token)
                .OrderByDescending(d => d.UpdatedAt)
                .FirstOrDefaultAsync(ct);
        }

        if (device is null)
        {
            device = new NotificationDevice(userId, token, platform, deviceId);
            await db.Set<NotificationDevice>().AddAsync(device, ct);
        }
        else
        {
            device.AssignTo(userId, token, platform, deviceId);
        }

        await db.SaveChangesAsync(ct);
        if (transaction is not null)
            await transaction.CommitAsync(ct);

        return device;
    }
}

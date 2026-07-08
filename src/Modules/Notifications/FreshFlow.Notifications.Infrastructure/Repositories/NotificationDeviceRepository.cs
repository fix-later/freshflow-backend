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
        var device = await db.Set<NotificationDevice>()
            .FirstOrDefaultAsync(d => d.UserId == userId && d.Token == normalizedToken, ct);
        var isInsert = device is null;

        if (device is null)
        {
            device = new NotificationDevice(userId, normalizedToken, platform, deviceId);
            await db.Set<NotificationDevice>().AddAsync(device, ct);
        }
        else
        {
            device.Reactivate(platform, deviceId);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (isInsert && IsUniqueViolation(ex))
        {
            db.Entry(device).State = EntityState.Detached;

            device = await db.Set<NotificationDevice>()
                .FirstOrDefaultAsync(d => d.UserId == userId && d.Token == normalizedToken, ct);

            if (device is null)
                throw;

            device.Reactivate(platform, deviceId);
            await db.SaveChangesAsync(ct);
        }

        return device;
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

    internal static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation;
}

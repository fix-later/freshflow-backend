using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Notifications.Infrastructure.Repositories;

internal sealed class NotificationRepository(AppDbContext db) : INotificationRepository
{
    public async Task<Notification> AddAsync(Notification notification, CancellationToken ct)
    {
        await db.Set<Notification>().AddAsync(notification, ct);
        await db.SaveChangesAsync(ct);
        return notification;
    }

    public async Task<(IReadOnlyList<Notification> Items, string? NextCursor)> GetPageAsync(
        Guid userId,
        string? cursor,
        int pageSize,
        bool? isRead,
        CancellationToken ct)
    {
        if (pageSize <= 0)
            throw new ArgumentException("pageSize must be greater than zero.", nameof(pageSize));

        var query = db.Set<Notification>()
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        if (isRead.HasValue)
            query = query.Where(n => n.IsRead == isRead.Value);

        var decoded = NotificationCursor.TryDecode(cursor);
        if (decoded is not null)
            query = query.Where(n =>
                n.CreatedAt < decoded.CreatedAt ||
                (n.CreatedAt == decoded.CreatedAt && n.Id < decoded.Id));

        var rows = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Take(pageSize + 1)
            .ToListAsync(ct);

        if (rows.Count <= pageSize)
            return (rows.AsReadOnly(), null);

        var page = rows.Take(pageSize).ToList().AsReadOnly();
        var nextCursor = NotificationCursor.Encode(page[^1].Id, page[^1].CreatedAt);
        return (page, nextCursor);
    }

    public async Task<Notification?> FindByIdForUserAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken ct) =>
        await db.Set<Notification>()
            .FirstOrDefaultAsync(n => n.UserId == userId && n.Id == notificationId, ct);

    public async Task<Notification?> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct)
    {
        var notification = await FindByIdForUserAsync(userId, notificationId, ct);
        if (notification is null)
            return null;

        notification.MarkRead();
        await db.SaveChangesAsync(ct);
        return notification;
    }

    private sealed record NotificationCursor(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("createdAt")] DateTime CreatedAt)
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public static NotificationCursor? TryDecode(string? encoded)
        {
            if (string.IsNullOrEmpty(encoded))
                return null;

            try
            {
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                return JsonSerializer.Deserialize<NotificationCursor>(json, Options);
            }
            catch
            {
                return null;
            }
        }

        public static string Encode(Guid id, DateTime createdAt)
        {
            var json = JsonSerializer.Serialize(new NotificationCursor(id, createdAt), Options);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }
    }
}

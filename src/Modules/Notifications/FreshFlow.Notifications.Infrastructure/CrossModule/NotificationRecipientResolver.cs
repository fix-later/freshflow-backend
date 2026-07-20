using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Notifications.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Notifications.Infrastructure.CrossModule;

internal sealed class NotificationRecipientResolver(AppDbContext db) : INotificationRecipientResolver
{
    public async Task<Guid?> ResolveUserIdByRestaurantIdAsync(Guid restaurantId, CancellationToken ct)
    {
        if (restaurantId == Guid.Empty)
            return null;

        return await db.Set<NotificationRecipientRow>()
            .AsNoTracking()
            .Where(r => r.RestaurantId == restaurantId)
            .Select(r => (Guid?)r.UserId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Guid?> ResolveUserIdByOrderIdAsync(Guid orderId, CancellationToken ct)
    {
        if (orderId == Guid.Empty)
            return null;

        return await db.Set<NotificationOrderRecipientRow>()
            .AsNoTracking()
            .Where(r => r.OrderId == orderId)
            .Select(r => (Guid?)r.UserId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<NotificationRecipient?> ResolveRecipientByRestaurantIdAsync(
        Guid restaurantId, CancellationToken ct)
    {
        if (restaurantId == Guid.Empty)
            return null;

        return await db.Set<NotificationRecipientRow>()
            .AsNoTracking()
            .Where(r => r.RestaurantId == restaurantId)
            .Select(r => new NotificationRecipient(r.UserId, r.Email))
            .FirstOrDefaultAsync(ct);
    }
}

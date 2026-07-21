namespace FreshFlow.Notifications.Application.Abstractions;

/// <summary>The owner user of a restaurant, with the email needed for email delivery.</summary>
public sealed record NotificationRecipient(Guid UserId, string Email);

public interface INotificationRecipientResolver
{
    public Task<Guid?> ResolveUserIdByRestaurantIdAsync(Guid restaurantId, CancellationToken ct);

    public Task<Guid?> ResolveUserIdByOrderIdAsync(Guid orderId, CancellationToken ct);

    /// <summary>
    /// Resolves both the owner user id and email for a restaurant in one read — used by
    /// consumers that deliver both in-app and by email. Null when the restaurant has no
    /// (non-deleted) owner user.
    /// </summary>
    public Task<NotificationRecipient?> ResolveRecipientByRestaurantIdAsync(
        Guid restaurantId, CancellationToken ct);
}

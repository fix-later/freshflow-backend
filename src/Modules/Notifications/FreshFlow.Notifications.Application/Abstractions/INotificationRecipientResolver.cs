namespace FreshFlow.Notifications.Application.Abstractions;

public interface INotificationRecipientResolver
{
    public Task<Guid?> ResolveUserIdByRestaurantIdAsync(Guid restaurantId, CancellationToken ct);
}

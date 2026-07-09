using FreshFlow.Notifications.Domain.Entities;

namespace FreshFlow.Notifications.Application.Abstractions;

public interface INotificationRepository
{
    public Task<Notification> AddAsync(Notification notification, CancellationToken ct);

    public Task UpdateAsync(Notification notification, CancellationToken ct);

    public Task<(IReadOnlyList<Notification> Items, string? NextCursor)> GetPageAsync(
        Guid userId,
        string? cursor,
        int pageSize,
        bool? isRead,
        CancellationToken ct);

    public Task<Notification?> FindByIdForUserAsync(Guid userId, Guid notificationId, CancellationToken ct);

    public Task<Notification?> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct);

    public Task<IReadOnlyList<Notification>> GetRetryablePageAsync(
        int maxAttempts,
        DateTime backoffThreshold,
        int batchSize,
        CancellationToken ct);
}

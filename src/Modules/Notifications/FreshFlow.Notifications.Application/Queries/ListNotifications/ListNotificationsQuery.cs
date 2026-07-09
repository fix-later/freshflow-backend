using FreshFlow.Notifications.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Notifications.Application.Queries.ListNotifications;

public sealed record ListNotificationsQuery(
    Guid UserId,
    string? Cursor = null,
    int PageSize = 50,
    bool? IsRead = null)
    : IQuery<NotificationPageDto>;

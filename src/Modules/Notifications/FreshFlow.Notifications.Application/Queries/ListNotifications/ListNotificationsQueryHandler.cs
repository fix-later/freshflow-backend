using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Application.Dtos;
using FreshFlow.Notifications.Application.Mappers;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Notifications.Application.Queries.ListNotifications;

internal sealed class ListNotificationsQueryHandler(INotificationRepository notifications)
    : IRequestHandler<ListNotificationsQuery, Result<NotificationPageDto>>
{
    public async Task<Result<NotificationPageDto>> Handle(
        ListNotificationsQuery request,
        CancellationToken ct)
    {
        var (items, nextCursor) = await notifications.GetPageAsync(
            request.UserId,
            request.Cursor,
            request.PageSize,
            request.IsRead,
            ct);

        var dtos = items
            .Select(notification => notification.ToDto())
            .ToList()
            .AsReadOnly();

        return Result<NotificationPageDto>.Success(
            new NotificationPageDto(dtos, request.PageSize, nextCursor));
    }
}

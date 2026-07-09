using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Application.Dtos;
using FreshFlow.Notifications.Application.Mappers;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Notifications.Application.Commands.MarkNotificationRead;

internal sealed class MarkNotificationReadCommandHandler(INotificationRepository notifications)
    : IRequestHandler<MarkNotificationReadCommand, Result<NotificationDto>>
{
    public async Task<Result<NotificationDto>> Handle(
        MarkNotificationReadCommand request,
        CancellationToken ct)
    {
        var notification = await notifications.MarkReadAsync(
            request.UserId,
            request.NotificationId,
            ct);

        return notification is null
            ? Result<NotificationDto>.Failure(Error.NotFound("NOTIFICATION", request.NotificationId))
            : Result<NotificationDto>.Success(notification.ToDto());
    }
}

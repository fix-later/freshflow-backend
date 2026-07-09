using FreshFlow.Notifications.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Notifications.Application.Commands.MarkNotificationRead;

public sealed record MarkNotificationReadCommand(Guid UserId, Guid NotificationId)
    : ICommand<NotificationDto>;

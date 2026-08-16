using FreshFlow.Notifications.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Notifications.Application.Commands.UnregisterDevice;

public sealed record UnregisterDeviceCommand(Guid UserId, string Token) : ICommand<NotificationDeviceDto>;

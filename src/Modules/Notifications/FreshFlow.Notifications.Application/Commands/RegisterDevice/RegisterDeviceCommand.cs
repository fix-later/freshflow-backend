using FreshFlow.Notifications.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Notifications.Application.Commands.RegisterDevice;

public sealed record RegisterDeviceCommand(
    Guid UserId,
    string Token,
    string Platform,
    string? DeviceId) : ICommand<NotificationDeviceDto>;

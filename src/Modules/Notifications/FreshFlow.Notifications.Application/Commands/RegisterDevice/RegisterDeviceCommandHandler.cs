using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Application.Dtos;
using FreshFlow.Notifications.Application.Mappers;
using FreshFlow.Notifications.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Notifications.Application.Commands.RegisterDevice;

internal sealed class RegisterDeviceCommandHandler(INotificationDeviceRepository devices)
    : IRequestHandler<RegisterDeviceCommand, Result<NotificationDeviceDto>>
{
    public async Task<Result<NotificationDeviceDto>> Handle(
        RegisterDeviceCommand request,
        CancellationToken ct)
    {
        if (request.UserId == Guid.Empty)
            return Result<NotificationDeviceDto>.Failure(
                Error.Validation("VALIDATION_ERROR", "User id is required."));

        if (string.IsNullOrWhiteSpace(request.Token))
            return Result<NotificationDeviceDto>.Failure(
                Error.Validation("VALIDATION_ERROR", "Token is required."));

        if (!NotificationDevicePlatformValues.TryParse(request.Platform, out var platform))
            return Result<NotificationDeviceDto>.Failure(
                Error.Validation("VALIDATION_ERROR", "Platform must be one of: ios, android, web."));

        var device = await devices.RegisterAsync(
            request.UserId,
            request.Token,
            platform,
            request.DeviceId,
            ct);

        return Result<NotificationDeviceDto>.Success(device.ToDto());
    }
}

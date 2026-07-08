using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Application.Dtos;
using FreshFlow.Notifications.Application.Mappers;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Notifications.Application.Commands.UnregisterDevice;

internal sealed class UnregisterDeviceCommandHandler(INotificationDeviceRepository devices)
    : IRequestHandler<UnregisterDeviceCommand, Result<NotificationDeviceDto>>
{
    public async Task<Result<NotificationDeviceDto>> Handle(
        UnregisterDeviceCommand request,
        CancellationToken ct)
    {
        if (request.UserId == Guid.Empty)
            return Result<NotificationDeviceDto>.Failure(
                Error.Validation("VALIDATION_ERROR", "User id is required."));

        if (string.IsNullOrWhiteSpace(request.Token))
            return Result<NotificationDeviceDto>.Failure(
                Error.Validation("VALIDATION_ERROR", "Token is required."));

        var device = await devices.UnregisterAsync(request.UserId, request.Token, ct);
        return device is null
            ? Result<NotificationDeviceDto>.Failure(Error.NotFound("NotificationDevice", "provided token"))
            : Result<NotificationDeviceDto>.Success(device.ToDto());
    }
}

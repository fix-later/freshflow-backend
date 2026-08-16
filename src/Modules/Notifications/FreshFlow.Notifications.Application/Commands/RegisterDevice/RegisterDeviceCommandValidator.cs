using FluentValidation;
using FreshFlow.Notifications.Domain.Enums;

namespace FreshFlow.Notifications.Application.Commands.RegisterDevice;

internal sealed class RegisterDeviceCommandValidator : AbstractValidator<RegisterDeviceCommand>
{
    public RegisterDeviceCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.Token).Must(t => !string.IsNullOrWhiteSpace(t))
            .WithMessage("Token is required.");
        RuleFor(c => c.Platform)
            .Must(NotificationDevicePlatformValues.IsSupported)
            .WithMessage("Platform must be one of: ios, android, web.");
    }
}

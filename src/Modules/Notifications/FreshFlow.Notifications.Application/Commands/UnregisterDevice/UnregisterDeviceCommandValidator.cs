using FluentValidation;

namespace FreshFlow.Notifications.Application.Commands.UnregisterDevice;

internal sealed class UnregisterDeviceCommandValidator : AbstractValidator<UnregisterDeviceCommand>
{
    public UnregisterDeviceCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.Token).Must(t => !string.IsNullOrWhiteSpace(t))
            .WithMessage("Token is required.");
    }
}

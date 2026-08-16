using FluentValidation;

namespace FreshFlow.Notifications.Application.Commands.MarkNotificationRead;

internal sealed class MarkNotificationReadCommandValidator
    : AbstractValidator<MarkNotificationReadCommand>
{
    public MarkNotificationReadCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.NotificationId).NotEmpty();
    }
}

using FluentValidation;

namespace FreshFlow.Auth.Application.Commands.VerifyEmail;

public sealed class VerifyEmailCommandValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailCommandValidator()
    {
        RuleFor(x => x.Identifier)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Channel)
            .NotEmpty();

        RuleFor(x => x.Code)
            .NotEmpty();
    }
}

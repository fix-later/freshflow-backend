using FluentValidation;

namespace FreshFlow.Auth.Application.Commands.RequestVerification;

public sealed class RequestVerificationCommandValidator : AbstractValidator<RequestVerificationCommand>
{
    public RequestVerificationCommandValidator()
    {
        RuleFor(x => x.Identifier)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Channel)
            .NotEmpty();
    }
}

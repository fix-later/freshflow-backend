using FluentValidation;

namespace FreshFlow.Auth.Application.Commands.Admin.UnlockUser;

public sealed class UnlockUserCommandValidator : AbstractValidator<UnlockUserCommand>
{
    public UnlockUserCommandValidator() =>
        RuleFor(x => x.UserId).NotEmpty();
}

using FluentValidation;

namespace FreshFlow.Auth.Application.Commands.Admin.ActivateUser;

public sealed class ActivateUserCommandValidator : AbstractValidator<ActivateUserCommand>
{
    public ActivateUserCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.RequestingAdminId).NotEmpty();
    }
}

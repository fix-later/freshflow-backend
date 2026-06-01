using FluentValidation;

namespace FreshFlow.Auth.Application.Commands.RefreshToken;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator() =>
        RuleFor(x => x.RefreshToken).NotEmpty();
}

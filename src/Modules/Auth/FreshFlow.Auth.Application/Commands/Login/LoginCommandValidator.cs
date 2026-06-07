using System.Text.RegularExpressions;
using FluentValidation;

namespace FreshFlow.Auth.Application.Commands.Login;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    // E.164-ish: optional leading +, then 7–15 digits
    private static readonly Regex PhoneRegex =
        new(@"^\+?[0-9]{7,15}$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    public LoginCommandValidator()
    {
        RuleFor(x => x.Identifier)
            .NotEmpty()
            .MaximumLength(255)
            .Must(id => IsEmail(id) || IsPhone(id))
            .WithMessage("Identifier must be a valid email address or phone number (7–15 digits, optional leading +).");

        RuleFor(x => x.Password)
            .NotEmpty();
    }

    private static bool IsEmail(string id) =>
        id.Contains('@', StringComparison.Ordinal);

    private static bool IsPhone(string id) =>
        PhoneRegex.IsMatch(id.Trim());
}

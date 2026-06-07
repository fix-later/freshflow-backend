using System.Text.RegularExpressions;
using FluentValidation;

namespace FreshFlow.Auth.Application.Commands.RegisterRestaurant;

public sealed class RegisterRestaurantCommandValidator : AbstractValidator<RegisterRestaurantCommand>
{
    private static readonly Regex PhoneRegex =
        new(@"^\+?[0-9]{7,15}$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    public RegisterRestaurantCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().EmailAddress().MaximumLength(255);

        RuleFor(x => x.Password)
            .NotEmpty().MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");

        RuleFor(x => x.RestaurantName)
            .NotEmpty().MaximumLength(200);

        RuleFor(x => x.Phone)
            .Must(p => p is null || PhoneRegex.IsMatch(p.Trim()))
            .WithMessage("Phone must be a valid phone number (7–15 digits, optional leading +).")
            .MaximumLength(20)
            .When(x => x.Phone is not null);
    }
}

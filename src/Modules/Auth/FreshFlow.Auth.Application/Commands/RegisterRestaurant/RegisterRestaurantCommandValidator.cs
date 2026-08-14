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

        // Tax code (mã số thuế) is optional at registration; when provided it must be a
        // valid Vietnam MST: 10 digits, with an optional 3-digit branch suffix.
        RuleFor(x => x.TaxCode)
            .Matches(@"^\d{10}(-\d{3})?$")
            .WithMessage("Tax code must be a valid Vietnam tax code (10 digits, optional -xxx branch suffix).")
            .MaximumLength(20)
            .When(x => !string.IsNullOrWhiteSpace(x.TaxCode));

        RuleFor(x => x.InvoiceLegalName)
            .NotEmpty()
            .MaximumLength(300)
            .When(x => x.InvoiceLegalName is not null);

        RuleFor(x => x.InvoiceAddress)
            .NotEmpty()
            .MaximumLength(256)
            .When(x => x.InvoiceAddress is not null);

        // Phone is optional; when provided the trimmed form must match the format.
        // Normalisation (trim + lowercase) happens in the domain: User.Create stores phone?.Trim().ToLowerInvariant().
        RuleFor(x => x.Phone)
            .Must(p => p is null || PhoneRegex.IsMatch(p.Trim()))
            .WithMessage("Phone must be a valid phone number (7–15 digits, optional leading +).")
            .MaximumLength(20)
            .When(x => x.Phone is not null);
    }
}

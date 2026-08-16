using System.Text.RegularExpressions;
using FluentValidation;

namespace FreshFlow.Auth.Application.Commands.DeliveryAddress.Add;

public sealed class AddDeliveryAddressCommandValidator : AbstractValidator<AddDeliveryAddressCommand>
{
    private static readonly Regex PhoneRegex =
        new(@"^\+?[0-9]{7,15}$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    public AddDeliveryAddressCommandValidator()
    {
        RuleFor(x => x.AddressLine)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(x => x.RecipientName)
            .MaximumLength(200)
            .When(x => x.RecipientName is not null);

        RuleFor(x => x.Phone)
            .Must(p => p is null || PhoneRegex.IsMatch(p.Trim()))
            .WithMessage("Phone must be a valid phone number (7–15 digits, optional leading +).")
            .MaximumLength(20)
            .When(x => x.Phone is not null);

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90m, 90m)
            .WithMessage("Latitude must be between -90 and 90.")
            .When(x => x.Latitude.HasValue);

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180m, 180m)
            .WithMessage("Longitude must be between -180 and 180.")
            .When(x => x.Longitude.HasValue);
    }
}

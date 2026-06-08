using System.Text.RegularExpressions;
using FluentValidation;

namespace FreshFlow.Auth.Application.Commands.UpdateMyProfile;

public sealed class UpdateMyProfileCommandValidator : AbstractValidator<UpdateMyProfileCommand>
{
    private static readonly Regex PhoneRegex =
        new(@"^\+?[0-9]{7,15}$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    public UpdateMyProfileCommandValidator()
    {
        RuleFor(x => x.FullName)
            .MaximumLength(255)
            .When(x => x.FullName is not null);

        RuleFor(x => x.Phone)
            .Must(p => p is null || PhoneRegex.IsMatch(p.Trim()))
            .WithMessage("Phone must be a valid phone number (7–15 digits, optional leading +).")
            .MaximumLength(20)
            .When(x => x.Phone is not null);

        RuleFor(x => x.AvatarUrl)
            .MaximumLength(512)
            .Must(url =>
                Uri.TryCreate(url, UriKind.Absolute, out var u) &&
                (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp))
            .WithMessage("AvatarUrl must be a valid absolute HTTPS or HTTP URL.")
            .When(x => x.AvatarUrl is not null);
    }
}

using FluentValidation;

namespace FreshFlow.Auth.Application.Commands.UpdateMyTaxProfile;

public sealed class UpdateMyTaxProfileCommandValidator : AbstractValidator<UpdateMyTaxProfileCommand>
{
    public UpdateMyTaxProfileCommandValidator()
    {
        RuleFor(x => x.TaxCode)
            .NotEmpty()
            .Matches(@"^\d{10}(-\d{3})?$")
            .MaximumLength(20);

        RuleFor(x => x.LegalName)
            .NotEmpty()
            .MaximumLength(300);

        RuleFor(x => x.Address)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(x => x.Email)
            .EmailAddress()
            .MaximumLength(256)
            .When(x => x.Email is not null);
    }
}

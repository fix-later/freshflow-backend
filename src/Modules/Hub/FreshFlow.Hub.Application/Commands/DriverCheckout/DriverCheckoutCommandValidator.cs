using FluentValidation;

namespace FreshFlow.Hub.Application.Commands.DriverCheckout;

internal sealed class DriverCheckoutCommandValidator : AbstractValidator<DriverCheckoutCommand>
{
    public DriverCheckoutCommandValidator()
    {
        RuleFor(x => x.HubId).NotEmpty();
        RuleFor(x => x.HandoverId).NotEmpty();
        RuleFor(x => x.DriverUserId).NotEmpty();
    }
}

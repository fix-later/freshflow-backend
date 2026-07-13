using FluentValidation;

namespace FreshFlow.Logistics.Application.Commands.ConfirmPickup;

internal sealed class ConfirmPickupCommandValidator : AbstractValidator<ConfirmPickupCommand>
{
    public ConfirmPickupCommandValidator()
    {
        RuleFor(x => x.RouteId).NotEmpty();
        RuleFor(x => x.DriverUserId).NotEmpty();
        RuleFor(x => x.OrderIds).NotEmpty();
        RuleForEach(x => x.OrderIds).NotEmpty();
        RuleFor(x => x.OrderIds)
            .Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
            .WithMessage("OrderIds must not contain duplicates.");
    }
}

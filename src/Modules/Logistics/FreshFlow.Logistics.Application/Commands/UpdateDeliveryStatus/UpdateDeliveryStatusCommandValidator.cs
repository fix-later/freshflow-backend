using FluentValidation;

namespace FreshFlow.Logistics.Application.Commands.UpdateDeliveryStatus;

internal sealed class UpdateDeliveryStatusCommandValidator : AbstractValidator<UpdateDeliveryStatusCommand>
{
    private static readonly HashSet<string> AllowedStatuses = ["ARRIVED", "DELIVERED", "FAILED"];

    public UpdateDeliveryStatusCommandValidator()
    {
        RuleFor(x => x.DeliveryId).NotEmpty();
        RuleFor(x => x.DriverUserId).NotEmpty();
        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(AllowedStatuses.Contains)
            .WithMessage("Status must be ARRIVED, DELIVERED, or FAILED.");
        RuleFor(x => x.FailureReason)
            .NotEmpty()
            .When(x => x.Status == "FAILED");
    }
}

using FluentValidation;

namespace FreshFlow.Orders.Application.Commands.ApproveClaim;

internal sealed class ApproveClaimCommandValidator : AbstractValidator<ApproveClaimCommand>
{
    public ApproveClaimCommandValidator()
    {
        RuleFor(command => command.ReviewedBy).NotEmpty();
        RuleFor(command => command.ClaimId).NotEmpty();
        RuleFor(command => command.DecisionNote).MaximumLength(1_000);
    }
}

using FluentValidation;

namespace FreshFlow.Orders.Application.Commands.RejectClaim;

internal sealed class RejectClaimCommandValidator : AbstractValidator<RejectClaimCommand>
{
    public RejectClaimCommandValidator()
    {
        RuleFor(command => command.ReviewedBy).NotEmpty();
        RuleFor(command => command.ClaimId).NotEmpty();
        RuleFor(command => command.DecisionNote).NotEmpty().MaximumLength(1_000);
    }
}

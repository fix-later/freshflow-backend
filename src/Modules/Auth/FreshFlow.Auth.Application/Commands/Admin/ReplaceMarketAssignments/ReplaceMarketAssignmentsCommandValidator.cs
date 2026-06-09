using FluentValidation;

namespace FreshFlow.Auth.Application.Commands.Admin.ReplaceMarketAssignments;

internal sealed class ReplaceMarketAssignmentsCommandValidator
    : AbstractValidator<ReplaceMarketAssignmentsCommand>
{
    public ReplaceMarketAssignmentsCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();

        RuleForEach(x => x.MarketIds)
            .NotEmpty()
            .WithName("MarketIds");

        RuleFor(x => x.MarketIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Market IDs must be unique.")
            .When(x => x.MarketIds.Count > 0);
    }
}

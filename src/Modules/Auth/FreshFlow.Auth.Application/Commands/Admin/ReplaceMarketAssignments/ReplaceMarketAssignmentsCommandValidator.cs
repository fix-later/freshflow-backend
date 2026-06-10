using FluentValidation;

namespace FreshFlow.Auth.Application.Commands.Admin.ReplaceMarketAssignments;

internal sealed class ReplaceMarketAssignmentsCommandValidator
    : AbstractValidator<ReplaceMarketAssignmentsCommand>
{
    public ReplaceMarketAssignmentsCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();

        RuleFor(x => x.MarketIds).NotNull();

        RuleForEach(x => x.MarketIds)
            .NotEmpty()
            .WithName("MarketIds")
            .When(x => x.MarketIds is not null);

        RuleFor(x => x.MarketIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Market IDs must be unique.")
            .When(x => x.MarketIds is not null && x.MarketIds.Count > 0);
    }
}

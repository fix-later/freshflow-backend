using FluentValidation;

namespace FreshFlow.Analytics.Application.Queries.GetDemandTimeDistribution;

internal sealed class GetDemandTimeDistributionQueryValidator
    : AbstractValidator<GetDemandTimeDistributionQuery>
{
    public GetDemandTimeDistributionQueryValidator()
    {
        RuleFor(query => query)
            .Must(query => query.From <= query.To)
            .WithErrorCode("VALIDATION_ERROR")
            .WithMessage("'from' must be earlier than or equal to 'to'.");

        RuleFor(query => query)
            .Must(query => query.From > query.To || query.To.DayNumber - query.From.DayNumber <= 366)
            .WithErrorCode("VALIDATION_ERROR")
            .WithMessage("The requested range cannot exceed 366 days.");
    }
}

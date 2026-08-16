using FluentValidation;

namespace FreshFlow.Logistics.Application.Queries.GetRouteSuggestions;

internal sealed class GetRouteSuggestionsQueryValidator : AbstractValidator<GetRouteSuggestionsQuery>
{
    public GetRouteSuggestionsQueryValidator()
    {
        RuleFor(query => query.ServiceDate).NotEmpty();
        RuleFor(query => query.ServiceDate)
            .LessThan(DateOnly.MaxValue)
            .WithMessage("service_date is out of range.");
    }
}

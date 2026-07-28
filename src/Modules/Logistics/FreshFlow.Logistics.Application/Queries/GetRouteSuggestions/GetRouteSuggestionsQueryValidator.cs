using FluentValidation;

namespace FreshFlow.Logistics.Application.Queries.GetRouteSuggestions;

internal sealed class GetRouteSuggestionsQueryValidator : AbstractValidator<GetRouteSuggestionsQuery>
{
    public GetRouteSuggestionsQueryValidator()
    {
        RuleFor(query => query.ServiceDate).NotEmpty();
    }
}

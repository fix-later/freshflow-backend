using FluentValidation;

namespace FreshFlow.Hub.Application.Queries.ListHubs;

internal sealed class ListHubsQueryValidator : AbstractValidator<ListHubsQuery>
{
    public ListHubsQueryValidator()
    {
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}

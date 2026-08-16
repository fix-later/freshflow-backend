using FluentValidation;

namespace FreshFlow.Hub.Application.Queries.ListHandovers;

internal sealed class ListHandoversQueryValidator : AbstractValidator<ListHandoversQuery>
{
    public ListHandoversQueryValidator()
    {
        RuleFor(x => x.HubId).NotEmpty();
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}

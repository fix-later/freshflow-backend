using FluentValidation;

namespace FreshFlow.Logistics.Application.Queries.ListRoutes;

internal sealed class ListRoutesQueryValidator : AbstractValidator<ListRoutesQuery>
{
    public ListRoutesQueryValidator()
    {
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}

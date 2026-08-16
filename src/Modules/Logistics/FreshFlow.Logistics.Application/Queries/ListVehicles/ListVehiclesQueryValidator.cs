using FluentValidation;

namespace FreshFlow.Logistics.Application.Queries.ListVehicles;

internal sealed class ListVehiclesQueryValidator : AbstractValidator<ListVehiclesQuery>
{
    public ListVehiclesQueryValidator()
    {
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}

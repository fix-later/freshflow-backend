using FluentValidation;

namespace FreshFlow.Catalog.Application.Queries.PackingCodes.List;

internal sealed class ListPackingCodesQueryValidator : AbstractValidator<ListPackingCodesQuery>
{
    public ListPackingCodesQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}

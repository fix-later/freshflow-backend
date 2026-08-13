using FluentValidation;

namespace FreshFlow.Procurement.Application.Queries.GetMarketSessionTracking;

internal sealed class GetMarketSessionTrackingQueryValidator
    : AbstractValidator<GetMarketSessionTrackingQuery>
{
    public GetMarketSessionTrackingQueryValidator()
    {
        RuleFor(query => query.Id).NotEmpty();
        RuleFor(query => query.Page).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

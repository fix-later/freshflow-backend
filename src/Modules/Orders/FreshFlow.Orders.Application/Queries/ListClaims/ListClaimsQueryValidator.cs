using FluentValidation;

namespace FreshFlow.Orders.Application.Queries.ListClaims;

internal sealed class ListClaimsQueryValidator : AbstractValidator<ListClaimsQuery>
{
    public ListClaimsQueryValidator()
    {
        RuleFor(query => query.UserId).NotEmpty();
        RuleFor(query => query.RestaurantId)
            .NotEqual(Guid.Empty)
            .When(query => query.RestaurantId.HasValue);
        RuleFor(query => query.Status)
            .Must(value => OrderClaimQueryParsing.TryParseStatus(value, out _))
            .WithMessage("Status is invalid.");
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

using FluentValidation;

namespace FreshFlow.Orders.Application.Queries.GetRestaurantCredit;

internal sealed class GetRestaurantCreditQueryValidator : AbstractValidator<GetRestaurantCreditQuery>
{
    public GetRestaurantCreditQueryValidator()
    {
        RuleFor(q => q.UserId).NotEmpty();
        RuleFor(q => q.RestaurantId).NotEmpty();
    }
}

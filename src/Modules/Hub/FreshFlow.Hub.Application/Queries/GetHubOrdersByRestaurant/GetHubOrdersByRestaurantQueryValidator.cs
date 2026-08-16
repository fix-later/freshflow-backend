using FluentValidation;

namespace FreshFlow.Hub.Application.Queries.GetHubOrdersByRestaurant;

internal sealed class GetHubOrdersByRestaurantQueryValidator
    : AbstractValidator<GetHubOrdersByRestaurantQuery>
{
    public GetHubOrdersByRestaurantQueryValidator()
    {
        RuleFor(query => query.HubId).NotEmpty();
        RuleFor(query => query.ServiceDate)
            .NotEmpty()
            .LessThan(DateOnly.MaxValue)
            .WithMessage("service_date is out of range.");
    }
}

using FluentValidation;
using FreshFlow.Auth.Domain.Enums;

namespace FreshFlow.Auth.Application.Queries.GetUsers;

public sealed class GetUsersQueryValidator : AbstractValidator<GetUsersQuery>
{
    public GetUsersQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.RestaurantStatus)
            .Must(status => Enum.TryParse<RestaurantStatus>(status, ignoreCase: true, out _))
            .When(x => x.RestaurantStatus is not null)
            .WithMessage("RestaurantStatus must be one of: pending, active, suspended.");
    }
}

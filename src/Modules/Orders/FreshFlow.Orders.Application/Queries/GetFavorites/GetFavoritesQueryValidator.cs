using FluentValidation;

namespace FreshFlow.Orders.Application.Queries.GetFavorites;

internal sealed class GetFavoritesQueryValidator : AbstractValidator<GetFavoritesQuery>
{
    public GetFavoritesQueryValidator()
    {
        RuleFor(q => q.UserId).NotEmpty();
    }
}

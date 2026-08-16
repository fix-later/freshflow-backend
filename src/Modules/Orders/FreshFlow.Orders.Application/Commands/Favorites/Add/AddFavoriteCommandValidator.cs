using FluentValidation;

namespace FreshFlow.Orders.Application.Commands.Favorites.Add;

internal sealed class AddFavoriteCommandValidator : AbstractValidator<AddFavoriteCommand>
{
    public AddFavoriteCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.MarketProductId).NotEmpty();
    }
}

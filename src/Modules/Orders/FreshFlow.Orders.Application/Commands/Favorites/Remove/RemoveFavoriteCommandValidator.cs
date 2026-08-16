using FluentValidation;

namespace FreshFlow.Orders.Application.Commands.Favorites.Remove;

internal sealed class RemoveFavoriteCommandValidator : AbstractValidator<RemoveFavoriteCommand>
{
    public RemoveFavoriteCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.MarketProductId).NotEmpty();
    }
}

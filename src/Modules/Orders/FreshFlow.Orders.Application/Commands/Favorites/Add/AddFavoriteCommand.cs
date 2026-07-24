using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Commands.Favorites.Add;

/// <summary>SCRUM-368 — POST /api/v1/restaurants/me/favorites.</summary>
public sealed record AddFavoriteCommand(
    Guid UserId,
    Guid MarketProductId) : ICommand<AddFavoriteResponse>;

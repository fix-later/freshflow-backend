using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Commands.Favorites.Remove;

/// <summary>SCRUM-368 — DELETE /api/v1/restaurants/me/favorites/{marketProductId}.</summary>
public sealed record RemoveFavoriteCommand(
    Guid UserId,
    Guid MarketProductId) : ICommand;

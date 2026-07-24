using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Queries.GetFavorites;

/// <summary>SCRUM-368 — GET /api/v1/restaurants/me/favorites.</summary>
public sealed record GetFavoritesQuery(Guid UserId) : IQuery<IReadOnlyList<FavoriteItemDto>>;

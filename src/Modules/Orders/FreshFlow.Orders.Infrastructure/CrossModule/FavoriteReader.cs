using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Orders.Infrastructure.CrossModule;

internal sealed class FavoriteReader(AppDbContext db) : IFavoriteReader
{
    public async Task<IReadOnlyList<FavoriteItemDto>> ListAsync(Guid restaurantId, CancellationToken ct)
    {
        var rows = await db.Set<FavoriteListItemRow>()
            .AsNoTracking()
            .Where(r => r.RestaurantId == restaurantId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return rows.Select(r => new FavoriteItemDto(
                r.MarketProductId,
                r.ProductId,
                r.ProductName,
                r.ImageUrl,
                r.MarketId,
                r.MarketName,
                r.Category,
                r.Unit ?? string.Empty,
                r.CurrentPrice,
                r.AvailableQuantity,
                r.CreatedAt))
            .ToList()
            .AsReadOnly();
    }
}

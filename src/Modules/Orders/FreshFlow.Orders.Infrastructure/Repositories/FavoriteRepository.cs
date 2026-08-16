using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Orders.Infrastructure.Repositories;

internal sealed class FavoriteRepository(AppDbContext db) : IFavoriteRepository
{
    public async Task<bool> AddAsync(RestaurantFavorite favorite, CancellationToken ct)
    {
        await db.Set<RestaurantFavorite>().AddAsync(favorite, ct);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (CreditRepository.IsUniqueViolation(ex))
        {
            // Lost the idempotency race against the unique index — already favorited.
            db.Entry(favorite).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<bool> RemoveAsync(Guid restaurantId, Guid marketProductId, CancellationToken ct)
    {
        var favorite = await db.Set<RestaurantFavorite>()
            .FirstOrDefaultAsync(
                f => f.RestaurantId == restaurantId && f.MarketProductId == marketProductId,
                ct);

        if (favorite is null)
            return false;

        db.Set<RestaurantFavorite>().Remove(favorite);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Lost the idempotency race — another request already removed this favorite.
            db.Entry(favorite).State = EntityState.Detached;
            return false;
        }
    }
}

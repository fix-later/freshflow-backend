using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Auth.Infrastructure.CrossModule;

internal sealed class RestaurantRepository(AppDbContext db) : IRestaurantRepository
{
    public async Task<Guid> CreateAsync(Guid userId, string restaurantName, CancellationToken ct)
    {
        var row = new RestaurantRow
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = restaurantName,
            IsApproved = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Set<RestaurantRow>().Add(row);
        await db.SaveChangesAsync(ct);
        return row.Id;
    }

    public async Task<RestaurantDto?> FindByIdAsync(Guid restaurantId, CancellationToken ct)
    {
        var row = await db.Set<RestaurantRow>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == restaurantId, ct);

        return row is null ? null : ToDto(row);
    }

    public async Task<RestaurantDto?> FindByUserIdAsync(Guid userId, CancellationToken ct)
    {
        var row = await db.Set<RestaurantRow>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId, ct);

        return row is null ? null : ToDto(row);
    }

    public async Task<bool> ApproveAsync(Guid restaurantId, CancellationToken ct)
    {
        var row = await db.Set<RestaurantRow>()
            .FirstOrDefaultAsync(r => r.Id == restaurantId, ct);

        if (row is null) return false;

        row.IsApproved = true;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static RestaurantDto ToDto(RestaurantRow row) =>
        new(row.Id, row.Name, row.IsApproved, row.UpdatedAt, row.UserId);
}

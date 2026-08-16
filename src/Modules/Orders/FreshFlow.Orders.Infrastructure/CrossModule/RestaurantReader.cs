using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Orders.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Orders.Infrastructure.CrossModule;

internal sealed class RestaurantReader(AppDbContext db) : IRestaurantReader
{
    private const string ActiveStatus = "active";

    public async Task<RestaurantSnapshotDto?> FindByUserIdAsync(Guid userId, CancellationToken ct)
    {
        var row = await db.Set<RestaurantRow>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId, ct);

        return row is null ? null : ToDto(row);
    }

    public async Task<RestaurantSnapshotDto?> FindByIdAsync(Guid restaurantId, CancellationToken ct)
    {
        var row = await db.Set<RestaurantRow>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == restaurantId, ct);

        return row is null ? null : ToDto(row);
    }

    public async Task<DeliveryAddressSourceDto?> FindDeliveryAddressAsync(
        Guid addressId, Guid restaurantId, CancellationToken ct)
    {
        var row = await db.Set<DeliveryAddressRow>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                address => address.Id == addressId && address.RestaurantId == restaurantId, ct);

        return row is null
            ? null
            : new DeliveryAddressSourceDto(
                row.Id, row.RecipientName, row.Phone, row.AddressLine, row.Latitude, row.Longitude);
    }

    private static RestaurantSnapshotDto ToDto(RestaurantRow row) =>
        new(row.Id, string.Equals(row.Status, ActiveStatus, StringComparison.OrdinalIgnoreCase));
}

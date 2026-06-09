using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Auth.Infrastructure.CrossModule;

internal sealed class DeliveryAddressRepository(AppDbContext db) : IDeliveryAddressRepository
{
    public async Task<DeliveryAddressDto> AddAsync(
        Guid restaurantId,
        string? recipientName,
        string? phone,
        string addressLine,
        decimal? latitude,
        decimal? longitude,
        bool isDefault,
        CancellationToken ct)
    {
        var row = new DeliveryAddressRow
        {
            Id = Guid.NewGuid(),
            RestaurantId = restaurantId,
            RecipientName = recipientName,
            Phone = phone,
            AddressLine = addressLine,
            Latitude = latitude,
            Longitude = longitude,
            IsDefault = isDefault,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (isDefault)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await ClearDefaultsInternalAsync(restaurantId, ct);
            db.Set<DeliveryAddressRow>().Add(row);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return ToDto(row);
        }

        db.Set<DeliveryAddressRow>().Add(row);
        await db.SaveChangesAsync(ct);
        return ToDto(row);
    }

    public async Task<IReadOnlyList<DeliveryAddressDto>> GetByRestaurantIdAsync(
        Guid restaurantId, CancellationToken ct)
    {
        var rows = await db.Set<DeliveryAddressRow>()
            .AsNoTracking()
            .Where(a => a.RestaurantId == restaurantId && a.DeletedAt == null)
            .OrderByDescending(a => a.IsDefault)
            .ThenBy(a => a.CreatedAt)
            .ToListAsync(ct);
        return rows.ConvertAll(ToDto).AsReadOnly();
    }

    public async Task<DeliveryAddressDto?> FindByIdAndRestaurantIdAsync(
        Guid addressId, Guid restaurantId, CancellationToken ct)
    {
        var row = await db.Set<DeliveryAddressRow>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.Id == addressId && a.RestaurantId == restaurantId && a.DeletedAt == null, ct);
        return row is null ? null : ToDto(row);
    }

    public async Task<DeliveryAddressDto?> UpdateAsync(
        Guid addressId,
        Guid restaurantId,
        string? recipientName,
        string? phone,
        string addressLine,
        decimal? latitude,
        decimal? longitude,
        bool isDefault,
        CancellationToken ct)
    {
        var row = await db.Set<DeliveryAddressRow>()
            .FirstOrDefaultAsync(
                a => a.Id == addressId && a.RestaurantId == restaurantId && a.DeletedAt == null, ct);

        if (row is null)
            return null;

        row.RecipientName = recipientName;
        row.Phone = phone;
        row.AddressLine = addressLine;
        row.Latitude = latitude;
        row.Longitude = longitude;
        row.IsDefault = isDefault;
        row.UpdatedAt = DateTime.UtcNow;

        if (isDefault)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // Exclude addressId: EF identity map returns the same tracked instance for this row,
            // so including it in the clear sweep would set IsDefault=false and clobber our mutation.
            await ClearDefaultsInternalAsync(restaurantId, ct, excludeId: addressId);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return ToDto(row);
        }

        await db.SaveChangesAsync(ct);
        return ToDto(row);
    }

    public async Task SoftDeleteAsync(Guid addressId, Guid restaurantId, CancellationToken ct)
    {
        var row = await db.Set<DeliveryAddressRow>()
            .FirstOrDefaultAsync(a => a.Id == addressId && a.RestaurantId == restaurantId, ct);

        if (row is null)
            return;

        row.DeletedAt = DateTime.UtcNow;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task ClearDefaultsAsync(Guid restaurantId, CancellationToken ct)
    {
        await ClearDefaultsInternalAsync(restaurantId, ct);
        await db.SaveChangesAsync(ct);
    }

    // excludeId: when updating an existing row that is already the default, EF's identity map
    // will return the same tracked instance for that row during the Where query, causing
    // ClearDefaults to set its IsDefault=false and clobber the caller's IsDefault=true mutation.
    // Excluding the row-being-updated from the sweep prevents this silent data loss.
    private async Task ClearDefaultsInternalAsync(
        Guid restaurantId, CancellationToken ct, Guid? excludeId = null)
    {
        var query = db.Set<DeliveryAddressRow>()
            .Where(a => a.RestaurantId == restaurantId && a.IsDefault && a.DeletedAt == null);

        if (excludeId.HasValue)
            query = query.Where(a => a.Id != excludeId.Value);

        var defaults = await query.ToListAsync(ct);

        foreach (var row in defaults)
        {
            row.IsDefault = false;
            row.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static DeliveryAddressDto ToDto(DeliveryAddressRow row) =>
        new(row.Id, row.RestaurantId, row.RecipientName, row.Phone,
            row.AddressLine, row.Latitude, row.Longitude,
            row.IsDefault, row.CreatedAt, row.UpdatedAt);
}

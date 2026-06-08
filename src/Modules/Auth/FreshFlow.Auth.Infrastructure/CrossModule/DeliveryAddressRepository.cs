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
        db.Set<DeliveryAddressRow>().Add(row);
        await db.SaveChangesAsync(ct);
        return ToDto(row);
    }

    public async Task<IReadOnlyList<DeliveryAddressDto>> GetByRestaurantIdAsync(
        Guid restaurantId, CancellationToken ct)
    {
        var rows = await db.Set<DeliveryAddressRow>()
            .AsNoTracking()
            .Where(a => a.RestaurantId == restaurantId)
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
                a => a.Id == addressId && a.RestaurantId == restaurantId, ct);
        return row is null ? null : ToDto(row);
    }

    public async Task<DeliveryAddressDto> UpdateAsync(
        Guid addressId,
        string? recipientName,
        string? phone,
        string addressLine,
        decimal? latitude,
        decimal? longitude,
        bool isDefault,
        CancellationToken ct)
    {
        var row = await db.Set<DeliveryAddressRow>()
            .FirstOrDefaultAsync(a => a.Id == addressId, ct)
            ?? throw new InvalidOperationException(
                $"DeliveryAddress '{addressId}' not found during update.");

        row.RecipientName = recipientName;
        row.Phone = phone;
        row.AddressLine = addressLine;
        row.Latitude = latitude;
        row.Longitude = longitude;
        row.IsDefault = isDefault;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(row);
    }

    public async Task SoftDeleteAsync(Guid addressId, CancellationToken ct)
    {
        var row = await db.Set<DeliveryAddressRow>()
            .FirstOrDefaultAsync(a => a.Id == addressId, ct)
            ?? throw new InvalidOperationException(
                $"DeliveryAddress '{addressId}' not found during soft-delete.");

        row.DeletedAt = DateTime.UtcNow;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task ClearDefaultsAsync(Guid restaurantId, CancellationToken ct)
    {
        var defaults = await db.Set<DeliveryAddressRow>()
            .Where(a => a.RestaurantId == restaurantId && a.IsDefault)
            .ToListAsync(ct);

        if (defaults.Count == 0) return;

        foreach (var row in defaults)
        {
            row.IsDefault = false;
            row.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
    }

    private static DeliveryAddressDto ToDto(DeliveryAddressRow row) =>
        new(row.Id, row.RestaurantId, row.RecipientName, row.Phone,
            row.AddressLine, row.Latitude, row.Longitude,
            row.IsDefault, row.CreatedAt, row.UpdatedAt);
}

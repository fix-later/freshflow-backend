using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Domain.Enums;
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
            Status = RestaurantStatus.Pending,
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

        row.Status = RestaurantStatus.Active;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> SuspendAsync(Guid restaurantId, CancellationToken ct)
    {
        var row = await db.Set<RestaurantRow>()
            .FirstOrDefaultAsync(r => r.Id == restaurantId, ct);

        if (row is null) return false;

        row.Status = RestaurantStatus.Suspended;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<RestaurantDto?> UpdateProfileAsync(
        Guid restaurantId,
        string name,
        string? address,
        string? contactPerson,
        TimeOnly? pickupStart,
        TimeOnly? pickupEnd,
        string? businessLicenseUrl,
        CancellationToken ct)
    {
        var row = await db.Set<RestaurantRow>()
            .FirstOrDefaultAsync(r => r.Id == restaurantId, ct);

        if (row is null)
            return null;

        row.Name = name;
        row.Address = address;
        row.ContactPerson = contactPerson;
        row.PickupStart = pickupStart;
        row.PickupEnd = pickupEnd;
        row.BusinessLicenseUrl = businessLicenseUrl;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(row);
    }

    public async Task<RestaurantDto?> UpdateTaxProfileAsync(
        Guid restaurantId,
        string taxCode,
        string legalName,
        string? address,
        string? email,
        CancellationToken ct)
    {
        var row = await db.Set<RestaurantRow>()
            .FirstOrDefaultAsync(r => r.Id == restaurantId, ct);

        if (row is null)
            return null;

        row.TaxCode = taxCode;
        row.InvoiceLegalName = legalName;
        row.InvoiceAddress = address;
        row.InvoiceEmail = email;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(row);
    }

    private static RestaurantDto ToDto(RestaurantRow row) =>
        new(row.Id, row.Name, row.Status, row.UpdatedAt, row.UserId,
            row.Address, row.ContactPerson, row.PickupStart, row.PickupEnd, row.BusinessLicenseUrl,
            row.TaxCode, row.InvoiceLegalName, row.InvoiceAddress, row.InvoiceEmail);
}

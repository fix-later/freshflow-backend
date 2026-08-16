using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Invoicing.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Invoicing.Infrastructure.CrossModule;

internal sealed class RestaurantReader(AppDbContext db) : IRestaurantReader
{
    public async Task<RestaurantTaxProfile?> GetTaxProfileAsync(Guid restaurantId, CancellationToken ct)
    {
        var row = await db.Set<RestaurantTaxProfileRow>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == restaurantId, ct);

        return row is null
            ? null
            : new RestaurantTaxProfile(
                row.Id, row.Name, row.TaxCode, row.InvoiceLegalName, row.InvoiceAddress, row.InvoiceEmail);
    }

    public async Task<Guid?> FindRestaurantIdByUserIdAsync(Guid userId, CancellationToken ct)
    {
        var row = await db.Set<RestaurantTaxProfileRow>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId, ct);

        return row?.Id;
    }
}

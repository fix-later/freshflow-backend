using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Invoicing.Application.Abstractions;
using FreshFlow.Invoicing.Domain.Entities;
using FreshFlow.Invoicing.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Invoicing.Infrastructure.Persistence.Repositories;

internal sealed class InvoiceRepository(AppDbContext db) : IInvoiceRepository
{
    public Task<bool> ExistsForOrderAsync(Guid orderId, CancellationToken ct) =>
        db.Set<Invoice>().AsNoTracking().AnyAsync(i => i.OrderId == orderId, ct);

    public async Task AddAsync(Invoice invoice, CancellationToken ct) =>
        await db.Set<Invoice>().AddAsync(invoice, ct);

    public Task<Invoice?> FindByIdAsync(Guid invoiceId, CancellationToken ct) =>
        db.Set<Invoice>()
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);

    public async Task<IReadOnlyList<Invoice>> GetRetryablePageAsync(
        int maxAttempts, DateTime backoffThreshold, int batchSize, CancellationToken ct)
    {
        if (maxAttempts <= 0)
            throw new ArgumentException("maxAttempts must be greater than zero.", nameof(maxAttempts));
        if (batchSize <= 0)
            throw new ArgumentException("batchSize must be greater than zero.", nameof(batchSize));

        return await db.Set<Invoice>()
            .Include(i => i.Lines)
            .Where(i =>
                (i.Status == InvoiceStatus.Draft || i.Status == InvoiceStatus.PendingIssuance) &&
                i.RetryCount < maxAttempts &&
                i.UpdatedAt < backoffThreshold)
            .OrderBy(i => i.UpdatedAt)
            .ThenBy(i => i.Id)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<Invoice> Items, int Total)> ListAsync(
        Guid? restaurantId, InvoiceStatus? status, int skip, int take, CancellationToken ct)
    {
        var query = db.Set<Invoice>().AsNoTracking();

        if (restaurantId is not null)
            query = query.Where(i => i.RestaurantId == restaurantId);
        if (status is not null)
            query = query.Where(i => i.Status == status);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(i => i.CreatedAt)
            .ThenByDescending(i => i.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

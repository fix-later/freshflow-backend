using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Orders.Infrastructure.Repositories;

internal sealed class CreditRepository(AppDbContext db) : ICreditRepository
{
    public async Task<RestaurantCredit?> FindAccountAsync(Guid restaurantId, CancellationToken ct) =>
        await db.Set<RestaurantCredit>()
            .FirstOrDefaultAsync(c => c.RestaurantId == restaurantId, ct);

    public async Task AddAccountAsync(RestaurantCredit account, CancellationToken ct) =>
        await db.Set<RestaurantCredit>().AddAsync(account, ct);

    public void Track(RestaurantCredit account)
    {
        var entry = db.Entry(account);
        if (entry.State == EntityState.Detached)
        {
            db.Attach(account);
            entry = db.Entry(account);
        }

        if (entry.State != EntityState.Added)
            entry.State = EntityState.Modified;
    }

    public void AddTransaction(CreditTransaction transaction) =>
        db.Set<CreditTransaction>().Add(transaction);

    public async Task<IReadOnlyList<CreditTransaction>> GetTransactionsAsync(Guid restaurantId, CancellationToken ct) =>
        await db.Set<CreditTransaction>()
            .AsNoTracking()
            .Where(t => t.RestaurantId == restaurantId)
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.Id)
            .ToListAsync(ct);

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new CreditConcurrencyException(
                "The credit account was updated by another request. Please refresh and retry.", ex);
        }
    }
}

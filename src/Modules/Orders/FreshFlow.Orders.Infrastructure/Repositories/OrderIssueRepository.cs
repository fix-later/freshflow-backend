using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Domain.Entities;

namespace FreshFlow.Orders.Infrastructure.Repositories;

internal sealed class OrderIssueRepository(AppDbContext db) : IOrderIssueRepository
{
    public async Task AddAsync(OrderIssue issue, CancellationToken ct) =>
        await db.Set<OrderIssue>().AddAsync(issue, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

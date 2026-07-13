using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Domain.Entities;

namespace FreshFlow.Logistics.Infrastructure.Repositories;

internal sealed class DeliveryIssueRepository(AppDbContext db) : IDeliveryIssueRepository
{
    public async Task AddAsync(DeliveryIssue issue, CancellationToken ct) =>
        await db.Set<DeliveryIssue>().AddAsync(issue, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        db.SaveChangesAsync(ct);
}

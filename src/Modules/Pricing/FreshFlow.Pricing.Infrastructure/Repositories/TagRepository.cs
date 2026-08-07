using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Domain.Entities;
using FreshFlow.Pricing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Pricing.Infrastructure.Repositories;

internal sealed class TagRepository(AppDbContext db) : ITagRepository
{
    public Task<Tag?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Set<Tag>().AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<Tag?> FindByNameAsync(string normalizedName, CancellationToken ct) =>
        db.Set<Tag>().AsNoTracking().FirstOrDefaultAsync(t => t.Name == normalizedName, ct);

    public async Task<IReadOnlyList<Tag>> GetAllAsync(CancellationToken ct) =>
        await db.Set<Tag>().AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct);

    // Tracking (not AsNoTracking): SetMarketProductTags assigns the returned tags straight onto
    // a tracked MarketProduct.Tags collection in the same DbContext — a tracking query lets EF's
    // identity map return the same tracked instance for a tag already loaded via that Include,
    // instead of throwing on two instances with the same key.
    public async Task<IReadOnlyList<Tag>> FindByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await db.Set<Tag>().Where(t => ids.Contains(t.Id)).ToListAsync(ct);

    public async Task AddAsync(Tag tag, CancellationToken ct) =>
        await db.Set<Tag>().AddAsync(tag, ct);

    public Task ClearAssignmentsAsync(Guid tagId, CancellationToken ct) =>
        db.Set<MarketProductTagLink>().Where(l => l.TagId == tagId).ExecuteDeleteAsync(ct);

    public void Track(Tag tag) => db.Update(tag);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

using FreshFlow.Pricing.Domain.Entities;

namespace FreshFlow.Pricing.Application.Abstractions;

public interface ITagRepository
{
    public Task<Tag?> FindByIdAsync(Guid id, CancellationToken ct);

    public Task<Tag?> FindByNameAsync(string normalizedName, CancellationToken ct);

    public Task<IReadOnlyList<Tag>> GetAllAsync(CancellationToken ct);

    /// <summary>Loads the live (not soft-deleted) tags matching <paramref name="ids"/>.</summary>
    public Task<IReadOnlyList<Tag>> FindByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    public Task AddAsync(Tag tag, CancellationToken ct);

    /// <summary>Hard-deletes every <c>market_product_tags</c> row assigning this tag (untag
    /// everywhere). Called by <c>DeleteTagCommand</c> alongside the tag's own soft-delete.</summary>
    public Task ClearAssignmentsAsync(Guid tagId, CancellationToken ct);

    public void Track(Tag tag);

    public Task SaveChangesAsync(CancellationToken ct);
}

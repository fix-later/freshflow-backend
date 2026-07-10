using FreshFlow.Hub.Domain.Entities;

namespace FreshFlow.Hub.Application.Abstractions;

public interface IHubOutboundRepository
{
    public Task AddAsync(HubOutboundEvent outbound, CancellationToken ct);
    public Task SaveChangesAsync(CancellationToken ct);
    public Task<(IReadOnlyList<HubOutboundEvent> Items, string? NextCursor, decimal TotalQuantityKg)> GetHistoryPageAsync(
        Guid hubId,
        DateOnly? date,
        string? cursor,
        int pageSize,
        CancellationToken ct);
}

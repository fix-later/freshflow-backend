using FreshFlow.Hub.Domain.Entities;

namespace FreshFlow.Hub.Application.Abstractions;

public interface IHubInboundRepository
{
    public Task AddAsync(HubInboundEvent inbound, CancellationToken ct);
    public Task SaveChangesAsync(CancellationToken ct);
    public Task<HubInboundEvent?> FindPendingByIdAsync(Guid id, CancellationToken ct);
    public Task<bool> DeliveryScheduleExistsAsync(Guid hubId, Guid deliveryScheduleId, CancellationToken ct);
    public Task<(IReadOnlyList<HubInboundEvent> Items, string? NextCursor)> GetPendingPageAsync(
        Guid hubId,
        string? cursor,
        int pageSize,
        CancellationToken ct);

    public Task<(IReadOnlyList<HubInboundEvent> Items, string? NextCursor, decimal TotalQuantityKg)> GetHistoryPageAsync(
        Guid hubId,
        DateOnly? date,
        string? cursor,
        int pageSize,
        CancellationToken ct);
}

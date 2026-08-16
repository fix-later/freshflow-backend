using FreshFlow.Logistics.Domain.Entities;

namespace FreshFlow.Logistics.Application.Abstractions;

public interface IRoutePlanRepository
{
    public Task<RoutePlan?> FindByIdAsync(Guid id, CancellationToken ct);
    public Task<RoutePlan?> FindProposedAsync(Guid marketSessionId, CancellationToken ct);
    public Task<IReadOnlyList<DeliveryRoute>> GetRoutesAsync(Guid planId, CancellationToken ct);
    public Task AddAsync(RoutePlan plan, CancellationToken ct);
    public Task<bool> TrySaveChangesAsync(CancellationToken ct);
}

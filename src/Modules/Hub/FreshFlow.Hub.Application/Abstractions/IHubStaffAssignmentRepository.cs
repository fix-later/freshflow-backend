using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.Application.Abstractions;

public interface IHubStaffAssignmentRepository
{
    public Task<IReadOnlyList<Guid>> GetUserIdsByHubAsync(Guid hubId, CancellationToken ct);
    public Task<bool> IsAssignedAsync(Guid hubId, Guid userId, CancellationToken ct);
    public Task<IReadOnlyList<HubEntity>> GetActiveHubsByUserAsync(Guid userId, CancellationToken ct);
    public Task ReplaceAsync(Guid hubId, IReadOnlyCollection<Guid> userIds, CancellationToken ct);
}

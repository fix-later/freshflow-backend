namespace FreshFlow.Hub.Application.Abstractions;

public interface IHubDriverAssignmentRepository
{
    public Task<IReadOnlyList<Guid>> GetUserIdsByHubAsync(Guid hubId, CancellationToken ct);
    public Task ReplaceAsync(Guid hubId, IReadOnlyCollection<Guid> userIds, CancellationToken ct);
}

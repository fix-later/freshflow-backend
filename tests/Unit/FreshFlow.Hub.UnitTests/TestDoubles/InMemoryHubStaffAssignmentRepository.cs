using FreshFlow.Hub.Application.Abstractions;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.UnitTests.TestDoubles;

internal sealed class InMemoryHubStaffAssignmentRepository : IHubStaffAssignmentRepository
{
    private readonly Dictionary<Guid, HashSet<Guid>> _userIdsByHub = [];

    public bool AllowAll { get; set; }
    public IReadOnlyList<HubEntity> AssignedHubs { get; set; } = [];
    public int ReplaceCallCount { get; private set; }

    public Task<IReadOnlyList<Guid>> GetUserIdsByHubAsync(Guid hubId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Guid>>(
            _userIdsByHub.TryGetValue(hubId, out var userIds)
                ? userIds.Order().ToList().AsReadOnly()
                : []);

    public Task<bool> IsAssignedAsync(Guid hubId, Guid userId, CancellationToken ct) =>
        Task.FromResult(
            AllowAll ||
            _userIdsByHub.TryGetValue(hubId, out var userIds) && userIds.Contains(userId));

    public Task<IReadOnlyList<HubEntity>> GetActiveHubsByUserAsync(Guid userId, CancellationToken ct) =>
        Task.FromResult(AssignedHubs);

    public Task ReplaceAsync(
        Guid hubId,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken ct)
    {
        _userIdsByHub[hubId] = userIds.ToHashSet();
        ReplaceCallCount++;
        return Task.CompletedTask;
    }
}

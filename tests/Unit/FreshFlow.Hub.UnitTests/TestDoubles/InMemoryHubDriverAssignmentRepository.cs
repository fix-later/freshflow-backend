using FreshFlow.Hub.Application.Abstractions;

namespace FreshFlow.Hub.UnitTests.TestDoubles;

internal sealed class InMemoryHubDriverAssignmentRepository : IHubDriverAssignmentRepository
{
    private readonly Dictionary<Guid, HashSet<Guid>> _userIdsByHub = [];

    public int ReplaceCallCount { get; private set; }

    public Task<IReadOnlyList<Guid>> GetUserIdsByHubAsync(Guid hubId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Guid>>(
            _userIdsByHub.TryGetValue(hubId, out var userIds)
                ? userIds.Order().ToList().AsReadOnly()
                : []);

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

using FreshFlow.Hub.Application.Abstractions;

namespace FreshFlow.Hub.UnitTests.TestDoubles;

internal sealed class InMemoryHubStaffReader : IHubStaffReader
{
    public bool AllowAll { get; set; }
    public IReadOnlyList<HubStaffUserDto> Users { get; set; } = [];

    public Task<IReadOnlyList<HubStaffUserDto>> GetByIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<HubStaffUserDto>>(
            AllowAll
                ? userIds.Select(userId => new HubStaffUserDto(
                    userId,
                    "hub_staff",
                    true,
                    null)).ToList().AsReadOnly()
                : Users.Where(user => userIds.Contains(user.UserId)).ToList().AsReadOnly());
}

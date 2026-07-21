namespace FreshFlow.Hub.Application.Abstractions;

public interface IHubStaffReader
{
    public Task<IReadOnlyList<HubStaffUserDto>> GetByIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken ct);
}

public sealed record HubStaffUserDto(
    Guid UserId,
    string RoleName,
    bool IsActive,
    DateTime? DeletedAt);

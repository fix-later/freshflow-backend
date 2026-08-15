namespace FreshFlow.Logistics.Application.Abstractions;

public interface IDriverReader
{
    public Task<DriverDto?> FindByUserIdAsync(Guid userId, CancellationToken ct);

    public Task<bool> IsAssignedToHubAsync(Guid userId, Guid hubId, CancellationToken ct);

    public Task<IReadOnlyList<DriverDto>> ListEligibleAsync(Guid hubId, CancellationToken ct);
}

public sealed record DriverDto(
    Guid UserId,
    string? FullName,
    string Email,
    string RoleName,
    bool IsActive);

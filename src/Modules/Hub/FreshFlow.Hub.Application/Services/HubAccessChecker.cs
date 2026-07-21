using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Services;

public sealed class HubAccessChecker(
    IHubStaffAssignmentRepository assignments,
    IHubStaffReader staff)
{
    private const string HubStaffRole = "hub_staff";
    private const string AdminRole = "admin";
    private const string OperationsManagerRole = "operations_manager";

    public async Task<Error?> CheckAsync(
        Guid hubId,
        bool hubIsActive,
        Guid actorUserId,
        bool bypassHubAssignment,
        CancellationToken ct)
    {
        if (!hubIsActive)
            return Denied();

        var user = await GetCurrentUserAsync(actorUserId, ct);
        if (user is null)
            return Denied();

        if (bypassHubAssignment)
            return IsPrivileged(user.RoleName) ? null : Denied();

        if (user.RoleName != HubStaffRole)
            return Denied();

        return await assignments.IsAssignedAsync(hubId, actorUserId, ct)
            ? null
            : Denied();
    }

    public async Task<Error?> CheckStaffAsync(Guid userId, CancellationToken ct)
    {
        var user = await GetCurrentUserAsync(userId, ct);
        return user is { RoleName: HubStaffRole }
            ? null
            : Denied();
    }

    public async Task<Error?> CheckPrivilegedAsync(Guid userId, CancellationToken ct)
    {
        var user = await GetCurrentUserAsync(userId, ct);
        return user is not null && IsPrivileged(user.RoleName)
            ? null
            : Denied();
    }

    private async Task<HubStaffUserDto?> GetCurrentUserAsync(Guid userId, CancellationToken ct)
    {
        var user = (await staff.GetByIdsAsync([userId], ct)).SingleOrDefault();
        return user is { IsActive: true, DeletedAt: null } ? user : null;
    }

    private static bool IsPrivileged(string roleName) =>
        roleName is AdminRole or OperationsManagerRole;

    private static Error Denied() =>
        Error.Unauthorized("HUB_ACCESS_DENIED", "You do not have access to this hub.");
}

using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Hub.Application.Commands.ReplaceHubStaffAssignments;

internal sealed class ReplaceHubStaffAssignmentsCommandHandler(
    IHubRepository hubs,
    IHubStaffReader staff,
    IHubStaffAssignmentRepository assignments)
    : IRequestHandler<ReplaceHubStaffAssignmentsCommand, Result<HubStaffAssignmentsDto>>
{
    private const string HubStaffRole = "hub_staff";

    public async Task<Result<HubStaffAssignmentsDto>> Handle(
        ReplaceHubStaffAssignmentsCommand request,
        CancellationToken ct)
    {
        if (await hubs.FindByIdAsync(request.HubId, ct) is null)
            return Result<HubStaffAssignmentsDto>.Failure(Error.NotFound("HUB", request.HubId));

        var users = await staff.GetByIdsAsync(request.StaffUserIds, ct);
        var byId = users.ToDictionary(user => user.UserId);

        foreach (var userId in request.StaffUserIds)
        {
            if (!byId.TryGetValue(userId, out var user) || user.DeletedAt is not null)
                return Result<HubStaffAssignmentsDto>.Failure(Error.NotFound("USER", userId));

            if (!user.IsActive || user.RoleName != HubStaffRole)
            {
                return Result<HubStaffAssignmentsDto>.Failure(
                    Error.Validation(
                        "INVALID_ASSIGNMENT_TARGET",
                        $"User '{userId}' must be active and have the '{HubStaffRole}' role."));
            }
        }

        await assignments.ReplaceAsync(request.HubId, request.StaffUserIds, ct);
        return Result<HubStaffAssignmentsDto>.Success(
            new HubStaffAssignmentsDto(request.HubId, request.StaffUserIds));
    }
}

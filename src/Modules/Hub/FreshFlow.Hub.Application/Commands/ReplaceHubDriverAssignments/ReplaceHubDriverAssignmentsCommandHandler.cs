using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Hub.Application.Commands.ReplaceHubDriverAssignments;

internal sealed class ReplaceHubDriverAssignmentsCommandHandler(
    IHubRepository hubs,
    IHubStaffReader users,
    IHubDriverAssignmentRepository assignments)
    : IRequestHandler<ReplaceHubDriverAssignmentsCommand, Result<HubDriverAssignmentsDto>>
{
    private const string DriverRole = "driver";

    public async Task<Result<HubDriverAssignmentsDto>> Handle(
        ReplaceHubDriverAssignmentsCommand request,
        CancellationToken ct)
    {
        if (await hubs.FindByIdAsync(request.HubId, ct) is null)
            return Result<HubDriverAssignmentsDto>.Failure(Error.NotFound("HUB", request.HubId));

        // `IHubStaffReader` reads any user with their role, so it answers for
        // drivers too — what differs is which role this roster accepts.
        var candidates = await users.GetByIdsAsync(request.DriverUserIds, ct);
        var byId = candidates.ToDictionary(user => user.UserId);

        foreach (var userId in request.DriverUserIds)
        {
            if (!byId.TryGetValue(userId, out var user) || user.DeletedAt is not null)
                return Result<HubDriverAssignmentsDto>.Failure(Error.NotFound("USER", userId));

            if (!user.IsActive || user.RoleName != DriverRole)
            {
                return Result<HubDriverAssignmentsDto>.Failure(
                    Error.Validation(
                        "INVALID_ASSIGNMENT_TARGET",
                        $"User '{userId}' must be active and have the '{DriverRole}' role."));
            }
        }

        await assignments.ReplaceAsync(request.HubId, request.DriverUserIds, ct);
        return Result<HubDriverAssignmentsDto>.Success(
            new HubDriverAssignmentsDto(request.HubId, request.DriverUserIds));
    }
}

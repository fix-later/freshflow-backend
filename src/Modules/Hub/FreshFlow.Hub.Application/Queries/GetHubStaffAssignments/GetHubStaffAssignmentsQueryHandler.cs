using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Hub.Application.Queries.GetHubStaffAssignments;

internal sealed class GetHubStaffAssignmentsQueryHandler(
    IHubRepository hubs,
    IHubStaffAssignmentRepository assignments)
    : IRequestHandler<GetHubStaffAssignmentsQuery, Result<HubStaffAssignmentsDto>>
{
    public async Task<Result<HubStaffAssignmentsDto>> Handle(
        GetHubStaffAssignmentsQuery request,
        CancellationToken ct)
    {
        if (await hubs.FindByIdAsync(request.HubId, ct) is null)
            return Result<HubStaffAssignmentsDto>.Failure(Error.NotFound("HUB", request.HubId));

        var userIds = await assignments.GetUserIdsByHubAsync(request.HubId, ct);
        return Result<HubStaffAssignmentsDto>.Success(
            new HubStaffAssignmentsDto(request.HubId, userIds));
    }
}

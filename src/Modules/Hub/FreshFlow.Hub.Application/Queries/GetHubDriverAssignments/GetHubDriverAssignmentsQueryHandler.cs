using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Hub.Application.Queries.GetHubDriverAssignments;

internal sealed class GetHubDriverAssignmentsQueryHandler(
    IHubRepository hubs,
    IHubDriverAssignmentRepository assignments)
    : IRequestHandler<GetHubDriverAssignmentsQuery, Result<HubDriverAssignmentsDto>>
{
    public async Task<Result<HubDriverAssignmentsDto>> Handle(
        GetHubDriverAssignmentsQuery request,
        CancellationToken ct)
    {
        if (await hubs.FindByIdAsync(request.HubId, ct) is null)
            return Result<HubDriverAssignmentsDto>.Failure(Error.NotFound("HUB", request.HubId));

        var userIds = await assignments.GetUserIdsByHubAsync(request.HubId, ct);
        return Result<HubDriverAssignmentsDto>.Success(
            new HubDriverAssignmentsDto(request.HubId, userIds));
    }
}

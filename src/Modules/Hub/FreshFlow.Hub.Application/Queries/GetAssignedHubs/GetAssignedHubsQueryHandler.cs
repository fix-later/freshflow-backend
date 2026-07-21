using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Application.Mappings;
using FreshFlow.Hub.Application.Services;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Hub.Application.Queries.GetAssignedHubs;

internal sealed class GetAssignedHubsQueryHandler(
    IHubStaffAssignmentRepository assignments,
    HubAccessChecker access)
    : IRequestHandler<GetAssignedHubsQuery, Result<IReadOnlyList<HubDto>>>
{
    public async Task<Result<IReadOnlyList<HubDto>>> Handle(
        GetAssignedHubsQuery request,
        CancellationToken ct)
    {
        var accessError = await access.CheckStaffAsync(request.UserId, ct);
        if (accessError is not null)
            return Result<IReadOnlyList<HubDto>>.Failure(accessError);

        var hubs = await assignments.GetActiveHubsByUserAsync(request.UserId, ct);
        return Result<IReadOnlyList<HubDto>>.Success(
            hubs.Select(hub => hub.ToDto()).ToList().AsReadOnly());
    }
}

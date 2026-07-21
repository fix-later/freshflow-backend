using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Queries.GetHubStaffAssignments;

public sealed record GetHubStaffAssignmentsQuery(Guid HubId, Guid ActorUserId)
    : IQuery<HubStaffAssignmentsDto>, IHubManagementRequest;

using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Queries.GetHubDriverAssignments;

public sealed record GetHubDriverAssignmentsQuery(Guid HubId, Guid ActorUserId)
    : IQuery<HubDriverAssignmentsDto>, IHubManagementRequest;

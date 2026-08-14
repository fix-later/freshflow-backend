using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Commands.ReplaceHubDriverAssignments;

public sealed record ReplaceHubDriverAssignmentsCommand(
    Guid HubId,
    IReadOnlyList<Guid> DriverUserIds,
    Guid ActorUserId)
    : ICommand<HubDriverAssignmentsDto>, IHubManagementRequest;

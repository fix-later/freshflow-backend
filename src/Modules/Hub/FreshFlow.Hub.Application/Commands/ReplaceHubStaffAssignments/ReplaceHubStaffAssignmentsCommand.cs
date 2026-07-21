using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Commands.ReplaceHubStaffAssignments;

public sealed record ReplaceHubStaffAssignmentsCommand(
    Guid HubId,
    IReadOnlyList<Guid> StaffUserIds,
    Guid ActorUserId)
    : ICommand<HubStaffAssignmentsDto>, IHubManagementRequest;

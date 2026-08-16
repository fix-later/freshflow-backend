namespace FreshFlow.Hub.Application.Dtos;

public sealed record HubStaffAssignmentsDto(
    Guid HubId,
    IReadOnlyList<Guid> StaffUserIds);

namespace FreshFlow.Hub.Application.Dtos;

public sealed record HubDriverAssignmentsDto(
    Guid HubId,
    IReadOnlyList<Guid> DriverUserIds);

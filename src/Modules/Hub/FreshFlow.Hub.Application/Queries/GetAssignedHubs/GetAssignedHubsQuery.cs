using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Queries.GetAssignedHubs;

public sealed record GetAssignedHubsQuery(Guid UserId)
    : IQuery<IReadOnlyList<HubDto>>;

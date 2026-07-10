using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Queries.ListHubs;

public sealed record ListHubsQuery(
    string? Cursor = null,
    int PageSize = 50,
    bool? IsActive = null) : IQuery<HubPageDto>;

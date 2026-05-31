using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Auth.Application.Queries.GetUsers;

public sealed record GetUsersQuery(
    string? Role,
    bool? IsActive,
    string? Search,
    int Page = 1,
    int PageSize = 20) : IQuery<GetUsersResponse>;

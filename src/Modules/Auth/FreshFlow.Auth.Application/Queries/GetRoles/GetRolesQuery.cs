using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Auth.Application.Queries.GetRoles;

public sealed record GetRolesQuery : IQuery<IReadOnlyList<RoleDto>>;

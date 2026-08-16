using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Auth.Application.Commands.Admin.AssignRole;

public sealed record AssignRoleCommand(Guid UserId, string RoleName) : ICommand<AssignRoleResponse>;

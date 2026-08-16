namespace FreshFlow.Auth.Application.Commands.Admin.AssignRole;

public sealed record AssignRoleResponse(Guid Id, string Email, string? FullName, string Role);

namespace FreshFlow.Auth.Application.Queries.GetRoles;

/// <param name="IsActive">Always true in v1 — roles are seeded and not deactivated.</param>
public sealed record RoleDto(Guid Id, string Name, string Description, bool IsActive = true);

namespace FreshFlow.Auth.Application.Commands.Admin.ActivateUser;

public sealed record ActivateUserResponse(Guid Id, string Email, string Role, bool IsActive, DateTime UpdatedAt);

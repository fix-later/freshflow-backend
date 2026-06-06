namespace FreshFlow.Auth.Application.Commands.Admin.UnlockUser;

public sealed record UnlockUserResponse(Guid Id, string Email, string Role, DateTime UpdatedAt);

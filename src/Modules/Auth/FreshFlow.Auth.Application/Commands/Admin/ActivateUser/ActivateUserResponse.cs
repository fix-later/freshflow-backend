namespace FreshFlow.Auth.Application.Commands.Admin.ActivateUser;

public sealed record ActivateUserResponse(
    Guid Id,
    string Email,
    string? FullName,
    string Role,
    bool IsActive,
    DateTime UpdatedAt);

namespace FreshFlow.Auth.Application.Commands.Admin.CreateUser;

public sealed record CreateUserResponse(
    Guid Id,
    string Email,
    string? FullName,
    string Role,
    bool IsActive,
    DateTime CreatedAt);

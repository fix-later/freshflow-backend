namespace FreshFlow.Auth.Application.Commands.UpdateMyProfile;

public sealed record UpdateMyProfileResponse(
    Guid Id,
    string Email,
    string? FullName,
    string? Phone,
    string Role,
    string? AvatarUrl);

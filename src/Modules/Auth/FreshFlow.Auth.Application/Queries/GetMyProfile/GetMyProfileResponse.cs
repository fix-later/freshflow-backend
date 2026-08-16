namespace FreshFlow.Auth.Application.Queries.GetMyProfile;

public sealed record GetMyProfileResponse(
    Guid Id,
    string Email,
    string? FullName,
    string? Phone,
    string Role,
    string? AvatarUrl);

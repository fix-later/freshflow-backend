using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Auth.Application.Commands.UpdateMyProfile;

public sealed record UpdateMyProfileCommand(
    Guid UserId,
    string? FullName,
    string? Phone,
    string? AvatarUrl) : ICommand<UpdateMyProfileResponse>;

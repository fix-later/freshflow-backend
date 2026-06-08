using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Commands.UpdateMyProfile;

internal sealed class UpdateMyProfileCommandHandler(IUserRepository users)
    : IRequestHandler<UpdateMyProfileCommand, Result<UpdateMyProfileResponse>>
{
    public async Task<Result<UpdateMyProfileResponse>> Handle(
        UpdateMyProfileCommand request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(request.UserId, ct);
        if (user is null)
            return Result<UpdateMyProfileResponse>.Failure(
                Error.NotFound("User", request.UserId));

        // Phone uniqueness check — only when phone is changing
        var normalizedPhone = string.IsNullOrWhiteSpace(request.Phone)
            ? null
            : request.Phone.Trim().ToLowerInvariant();

        if (normalizedPhone is not null && normalizedPhone != user.Phone)
        {
            var taken = await users.ExistsByPhoneAsync(normalizedPhone, ct);
            if (taken)
                return Result<UpdateMyProfileResponse>.Failure(
                    Error.Conflict("PHONE_ALREADY_EXISTS",
                        "Phone number is already in use by another account."));
        }

        user.UpdateProfile(request.FullName, request.Phone, request.AvatarUrl);
        await users.SaveChangesAsync(ct);

        return Result<UpdateMyProfileResponse>.Success(
            new UpdateMyProfileResponse(
                user.Id,
                user.Email,
                user.FullName,
                user.Phone,
                user.Role.Name,
                user.AvatarUrl));
    }
}

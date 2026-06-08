using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Queries.GetMyProfile;

internal sealed class GetMyProfileQueryHandler(IUserRepository users)
    : IRequestHandler<GetMyProfileQuery, Result<GetMyProfileResponse>>
{
    public async Task<Result<GetMyProfileResponse>> Handle(
        GetMyProfileQuery request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(request.UserId, ct);
        if (user is null)
            return Result<GetMyProfileResponse>.Failure(
                Error.NotFound("User", request.UserId));

        return Result<GetMyProfileResponse>.Success(
            new GetMyProfileResponse(
                user.Id,
                user.Email,
                user.FullName,
                user.Phone,
                user.Role.Name,
                user.AvatarUrl));
    }
}

using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Commands.Admin.ActivateUser;

internal sealed class ActivateUserCommandHandler(IUserRepository users)
    : IRequestHandler<ActivateUserCommand, Result<ActivateUserResponse>>
{
    public async Task<Result<ActivateUserResponse>> Handle(ActivateUserCommand request, CancellationToken ct)
    {
        if (request.UserId == request.RequestingAdminId && !request.IsActive)
            return Result<ActivateUserResponse>.Failure(
                Error.Validation("CANNOT_DEACTIVATE_SELF", "Admin cannot deactivate their own account."));

        var user = await users.FindByIdAsync(request.UserId, ct);
        if (user is null)
            return Result<ActivateUserResponse>.Failure(
                Error.NotFound("User", request.UserId));

        if (request.IsActive) user.Activate();
        else user.Deactivate();

        await users.SaveChangesAsync(ct);

        return Result<ActivateUserResponse>.Success(
            new ActivateUserResponse(user.Id, user.Email,
                user.Role.Name, user.IsActive, user.UpdatedAt));
    }
}

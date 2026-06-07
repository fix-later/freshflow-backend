using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Commands.Admin.AssignRole;

internal sealed class AssignRoleCommandHandler(
    IUserRepository users,
    IRoleRepository roles,
    IRefreshTokenRepository tokens) : IRequestHandler<AssignRoleCommand, Result<AssignRoleResponse>>
{
    private static readonly Error InvalidRole =
        Error.Validation("INVALID_ROLE", "The specified role does not exist.");

    public async Task<Result<AssignRoleResponse>> Handle(AssignRoleCommand request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(request.UserId, ct);
        if (user is null)
            return Result<AssignRoleResponse>.Failure(Error.NotFound("User", request.UserId));

        var role = await roles.FindByNameAsync(request.RoleName, ct);
        if (role is null)
            return Result<AssignRoleResponse>.Failure(InvalidRole);

        user.AssignRole(role);

        // Revoke all active sessions so next login/refresh carries the updated role claim.
        await tokens.RevokeByUserAsync(request.UserId, ct);
        await users.SaveChangesAsync(ct);

        return Result<AssignRoleResponse>.Success(
            new AssignRoleResponse(user.Id, user.Email, role.Name));
    }
}

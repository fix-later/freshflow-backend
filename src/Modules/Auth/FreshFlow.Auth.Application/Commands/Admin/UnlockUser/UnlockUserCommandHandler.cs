using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Commands.Admin.UnlockUser;

internal sealed class UnlockUserCommandHandler(IUserRepository users)
    : IRequestHandler<UnlockUserCommand, Result>
{
    public async Task<Result> Handle(UnlockUserCommand request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(request.UserId, ct);
        if (user is null)
            return Result.Failure(Error.NotFound("User", request.UserId));

        user.Unlock();
        await users.SaveChangesAsync(ct);

        return Result.Success();
    }
}

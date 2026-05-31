using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Auth.Application.Commands.Admin.ActivateUser;

public sealed record ActivateUserCommand(Guid UserId, bool IsActive, Guid RequestingAdminId)
    : ICommand<ActivateUserResponse>;

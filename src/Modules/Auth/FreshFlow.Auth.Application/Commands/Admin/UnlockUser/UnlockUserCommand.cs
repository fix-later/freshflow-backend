using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Auth.Application.Commands.Admin.UnlockUser;

public sealed record UnlockUserCommand(Guid UserId) : ICommand;

using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Auth.Application.Commands.ChangePassword;

public sealed record ChangePasswordCommand(
    Guid UserId,
    string CurrentPassword,
    string NewPassword) : ICommand;

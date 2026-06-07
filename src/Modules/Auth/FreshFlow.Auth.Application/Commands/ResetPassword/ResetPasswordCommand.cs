using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Auth.Application.Commands.ResetPassword;

public sealed record ResetPasswordCommand(string Token, string NewPassword) : ICommand;

using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Auth.Application.Commands.ResetPassword;

public sealed record ResetPasswordCommand(string Identifier, string Code, string NewPassword) : ICommand;

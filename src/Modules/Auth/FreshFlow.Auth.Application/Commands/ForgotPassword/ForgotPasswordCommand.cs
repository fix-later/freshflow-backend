using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Auth.Application.Commands.ForgotPassword;

/// <param name="Identifier">The user's registered email address.</param>
public sealed record ForgotPasswordCommand(string Identifier) : ICommand;

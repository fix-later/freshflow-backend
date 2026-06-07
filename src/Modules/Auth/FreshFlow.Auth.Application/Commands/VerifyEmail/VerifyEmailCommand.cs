using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Auth.Application.Commands.VerifyEmail;

public sealed record VerifyEmailCommand(string Identifier, string Channel, string Code) : ICommand;

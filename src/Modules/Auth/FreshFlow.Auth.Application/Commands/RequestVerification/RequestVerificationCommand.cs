using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Auth.Application.Commands.RequestVerification;

public sealed record RequestVerificationCommand(string Identifier, string Channel) : ICommand;

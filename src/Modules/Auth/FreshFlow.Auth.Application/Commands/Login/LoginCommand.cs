using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Auth.Application.Commands.Login;

/// <summary>
/// <c>Identifier</c> can be either an email address or a phone number.
/// </summary>
public sealed record LoginCommand(string Identifier, string Password) : ICommand<LoginResponse>;

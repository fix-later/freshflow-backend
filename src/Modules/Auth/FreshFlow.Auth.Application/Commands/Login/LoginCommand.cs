using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Auth.Application.Commands.Login;

public sealed record LoginCommand(string Email, string Password) : ICommand<LoginResponse>;

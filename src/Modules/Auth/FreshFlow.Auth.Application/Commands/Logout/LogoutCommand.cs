using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Auth.Application.Commands.Logout;

public sealed record LogoutCommand(Guid UserId, string RefreshToken) : ICommand;

using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Auth.Application.Commands.RefreshToken;

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<RefreshTokenResponse>;

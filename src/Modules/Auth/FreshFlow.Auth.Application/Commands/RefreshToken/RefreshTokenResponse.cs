namespace FreshFlow.Auth.Application.Commands.RefreshToken;

public sealed record RefreshTokenResponse(string AccessToken, string RefreshToken, int ExpiresIn);

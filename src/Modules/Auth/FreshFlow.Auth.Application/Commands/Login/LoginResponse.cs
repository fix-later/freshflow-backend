namespace FreshFlow.Auth.Application.Commands.Login;

public sealed record LoginResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    LoginUserDto User);

public sealed record LoginUserDto(Guid Id, string Email, string Role);

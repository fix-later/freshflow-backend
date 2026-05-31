namespace FreshFlow.Auth.Application.Abstractions;

public interface ITokenService
{
    public string GenerateAccessToken(Guid userId, string email, string role);
    public string GenerateRefreshToken();
    public string HashRefreshToken(string rawToken);
    public int AccessTokenTtlSeconds { get; }
    public int RefreshTokenTtlDays { get; }
}

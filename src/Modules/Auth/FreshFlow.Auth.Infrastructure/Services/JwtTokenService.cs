using System.Security.Cryptography;
using System.Text;
using FreshFlow.Auth.Application.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FreshFlow.Auth.Infrastructure.Services;

public sealed class JwtTokenService(IOptions<JwtSettings> options) : ITokenService
{
    private readonly JwtSettings _settings = options.Value;

    public int AccessTokenTtlSeconds => _settings.AccessTokenTtlSeconds;
    public int RefreshTokenTtlDays => _settings.RefreshTokenTtlDays;

    public string GenerateAccessToken(Guid userId, string email, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;

        // Use SecurityTokenDescriptor.Claims dictionary (not Subject.ClaimsIdentity) so that
        // claim names are written verbatim — no OutboundClaimTypeMap transformations.
        // JWT body will contain: sub, email, role, iat, exp (exp-iat = AccessTokenTtlSeconds = 900).
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = userId.ToString(),
                [JwtRegisteredClaimNames.Email] = email,
                ["role"] = role
            },
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddSeconds(_settings.AccessTokenTtlSeconds),
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            SigningCredentials = credentials
        };

        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(tokenDescriptor);
    }

    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public string HashRefreshToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

using System.ComponentModel.DataAnnotations;

namespace FreshFlow.Auth.Infrastructure.Services;

public sealed class JwtSettings
{
    [Required, MinLength(32)]
    public string Key { get; init; } = string.Empty;

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    public int AccessTokenTtlSeconds { get; init; } = 900;
    public int RefreshTokenTtlDays { get; init; } = 7;
}

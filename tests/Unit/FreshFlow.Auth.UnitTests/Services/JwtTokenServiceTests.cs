using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using FreshFlow.Auth.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FreshFlow.Auth.UnitTests.Services;

[Trait("Category", "Unit")]
public sealed class JwtTokenServiceTests
{
    private readonly JwtTokenService _sut;
    private readonly JwtSettings _settings = new()
    {
        Key = "unit-test-secret-key-that-is-at-least-32-chars!!",
        Issuer = "https://test.freshflow",
        Audience = "freshflow-api",
        AccessTokenTtlSeconds = 900,
        RefreshTokenTtlDays = 7
    };

    public JwtTokenServiceTests()
    {
        _sut = new JwtTokenService(Options.Create(_settings));
    }

    [Fact]
    public void GenerateAccessToken_ContainsExpectedClaims()
    {
        var userId = Guid.NewGuid();
        var token = _sut.GenerateAccessToken(userId, "test@example.com", "admin");

        var handler = new JwtSecurityTokenHandler();
        handler.InboundClaimTypeMap.Clear(); // Prevent sub → NameIdentifier auto-mapping

        var validationParams = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _settings.Issuer,
            ValidateAudience = true,
            ValidAudience = _settings.Audience,
            ValidateLifetime = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key)),
            ClockSkew = TimeSpan.Zero
        };

        var principal = handler.ValidateToken(token, validationParams, out _);
        principal.FindFirstValue(JwtRegisteredClaimNames.Sub).Should().Be(userId.ToString());
        principal.FindFirstValue(JwtRegisteredClaimNames.Email).Should().Be("test@example.com");
        // Token carries the short "role" claim (not the full ClaimTypes.Role URI).
        principal.FindFirstValue("role").Should().Be("admin");
    }

    [Fact]
    public void GenerateAccessToken_ExpiresIn900Seconds()
    {
        var before = DateTime.UtcNow;
        var token = _sut.GenerateAccessToken(Guid.NewGuid(), "u@test.com", "driver");
        var after = DateTime.UtcNow;

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.ValidTo.Should().BeAfter(before.AddSeconds(899));
        jwt.ValidTo.Should().BeBefore(after.AddSeconds(901));
    }

    [Fact]
    public void GenerateAccessToken_TamperedToken_FailsValidation()
    {
        var token = _sut.GenerateAccessToken(Guid.NewGuid(), "u@test.com", "hub_staff");
        var tampered = token[..^5] + "XXXXX";

        var handler = new JwtSecurityTokenHandler();
        var validationParams = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _settings.Issuer,
            ValidateAudience = true,
            ValidAudience = _settings.Audience,
            ValidateLifetime = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key))
        };

        var act = () => handler.ValidateToken(tampered, validationParams, out _);
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void GenerateRefreshToken_Returns64HexChars()
    {
        var token = _sut.GenerateRefreshToken();
        token.Should().HaveLength(64);
        token.Should().MatchRegex("^[0-9a-f]+$");
    }

    [Fact]
    public void GenerateRefreshToken_TwoCalls_ProduceDifferentTokens()
    {
        var t1 = _sut.GenerateRefreshToken();
        var t2 = _sut.GenerateRefreshToken();
        t1.Should().NotBe(t2);
    }

    [Fact]
    public void HashRefreshToken_SameInput_ProducesSameHash()
    {
        const string raw = "abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";
        var h1 = _sut.HashRefreshToken(raw);
        var h2 = _sut.HashRefreshToken(raw);
        h1.Should().Be(h2);
    }

    [Fact]
    public void HashRefreshToken_DifferentInput_ProducesDifferentHash()
    {
        var h1 = _sut.HashRefreshToken(_sut.GenerateRefreshToken());
        var h2 = _sut.HashRefreshToken(_sut.GenerateRefreshToken());
        h1.Should().NotBe(h2);
    }
}

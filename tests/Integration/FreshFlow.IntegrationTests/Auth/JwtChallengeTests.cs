using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using FreshFlow.IntegrationTests.Infrastructure;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FreshFlow.IntegrationTests.Auth;

/// <summary>
/// UC-AUTH-08 / FR-AUTH-008 — JWT challenge contract.
/// Verifies that the JWT middleware returns a JSON envelope { success, error: { code, message } } body
/// instead of the default WWW-Authenticate plain-text challenge.
/// </summary>
[Trait("Category", "Integration")]
public sealed class JwtChallengeTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private const string TestJwtKey = "integration-test-secret-key-min-32-chars!!";
    private const string TestIssuer = "https://test.freshflow";
    private const string TestAudience = "freshflow-api";

    // Protected endpoint that requires ADMIN role — used for all challenge scenarios.
    private const string ProtectedPath = "/api/v1/admin/users";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ProtectedEndpoint_WithExpiredToken_Returns401WithTokenExpiredCode()
    {
        // Arrange
        var expiredToken = CreateExpiredToken();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", expiredToken);

        // Act
        var response = await _client.GetAsync(ProtectedPath);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var env = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        env.Should().NotBeNull();
        env!.Error!.Code.Should().Be("TOKEN_EXPIRED");
        env.Error.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ProtectedEndpoint_WithTamperedToken_Returns401WithUnauthorizedCode()
    {
        // Arrange — valid JWT structure but signed with the wrong secret
        var tamperedToken = CreateTamperedToken();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tamperedToken);

        // Act
        var response = await _client.GetAsync(ProtectedPath);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var env = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        env.Should().NotBeNull();
        env!.Error!.Code.Should().Be("UNAUTHORIZED");
        env.Error.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ProtectedEndpoint_WithMissingToken_Returns401WithUnauthorizedCode()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.GetAsync(ProtectedPath);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var env = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        env.Should().NotBeNull();
        env!.Error!.Code.Should().Be("UNAUTHORIZED");
        env.Error.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ProtectedEndpoint_WithValidAdminToken_ReachesHandler()
    {
        // Arrange — obtain a fresh valid access token via login
        var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            identifier = "admin@test.freshflow",
            password = "AdminP@ss1"
        });
        loginResp.EnsureSuccessStatusCode();
        var env = await loginResp.Content.ReadFromJsonAsync<Envelope<TokenBody>>();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", env!.Data!.AccessToken);

        // Act
        var response = await _client.GetAsync(ProtectedPath);

        // Assert — JWT middleware passed the request through; handler returned 200
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
            because: "a valid token must not be rejected by the JWT challenge");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static string CreateExpiredToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtKey));
        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, "admin")
            ]),
            NotBefore = DateTime.UtcNow.AddMinutes(-10),
            IssuedAt = DateTime.UtcNow.AddMinutes(-10),
            Expires = DateTime.UtcNow.AddSeconds(-60), // expired 60 s ago
            Issuer = TestIssuer,
            Audience = TestAudience,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        });
    }

    /// <summary>
    /// Creates a structurally valid JWT signed with the wrong secret key.
    /// The validator will reject it with an invalid-signature failure.
    /// </summary>
    private static string CreateTamperedToken()
    {
        var wrongKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("wrong-secret-key-that-does-not-match-32-chars!"));
        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, "admin")
            ]),
            Expires = DateTime.UtcNow.AddMinutes(15),
            Issuer = TestIssuer,
            Audience = TestAudience,
            SigningCredentials = new SigningCredentials(wrongKey, SecurityAlgorithms.HmacSha256)
        });
    }
}

// Shared envelope types are in Infrastructure/ApiEnvelopes.cs

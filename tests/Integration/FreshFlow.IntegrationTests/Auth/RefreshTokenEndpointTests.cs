using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.IntegrationTests.Infrastructure;

namespace FreshFlow.IntegrationTests.Auth;

[Trait("Category", "Integration")]
public sealed class RefreshTokenEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(string Access, string Refresh)> LoginAsAdminAsync()
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            identifier = "admin@test.freshflow",
            password = "AdminP@ss1"
        });
        resp.EnsureSuccessStatusCode();
        var env = await resp.Content.ReadFromJsonAsync<Envelope<TokenBody>>();
        return (env!.Data!.AccessToken, env.Data.RefreshToken);
    }

    [Fact]
    public async Task Refresh_WithValidToken_Returns200WithNewPair()
    {
        var (_, refresh) = await LoginAsAdminAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = refresh });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await response.Content.ReadFromJsonAsync<Envelope<TokenBody>>();
        env!.Success.Should().BeTrue();
        env.Data!.AccessToken.Should().NotBeNullOrEmpty();
        env.Data.RefreshToken.Should().NotBe(refresh, "token should be rotated");
    }

    [Fact]
    public async Task Refresh_WithOldToken_Returns409Conflict()
    {
        var (_, refresh) = await LoginAsAdminAsync();
        // First refresh — rotates token
        var firstResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = refresh });
        firstResponse.IsSuccessStatusCode.Should().BeTrue("first refresh with a valid token must succeed");
        // Second refresh with the old token — token reuse detected
        var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = refresh });

        // FR-AUTH-007 AC2: reuse must return 409 with REFRESH_TOKEN_REUSE code
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var env = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        env!.Success.Should().BeFalse();
        env.Error!.Code.Should().Be("REFRESH_TOKEN_REUSE");
    }

    [Fact]
    public async Task Refresh_WithGarbageToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = "totally-invalid-token" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

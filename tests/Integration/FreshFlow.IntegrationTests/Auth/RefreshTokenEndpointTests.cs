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
        var body = await resp.Content.ReadFromJsonAsync<TokenPair>();
        return (body!.AccessToken, body.RefreshToken);
    }

    [Fact]
    public async Task Refresh_WithValidToken_Returns200WithNewPair()
    {
        var (_, refresh) = await LoginAsAdminAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = refresh });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TokenPair>();
        body!.AccessToken.Should().NotBeNullOrEmpty();
        body.RefreshToken.Should().NotBe(refresh, "token should be rotated");
    }

    [Fact]
    public async Task Refresh_WithOldToken_Returns401OrConflict()
    {
        var (_, refresh) = await LoginAsAdminAsync();
        // First refresh — rotates token
        await _client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = refresh });
        // Second refresh with the old token — should be rejected
        var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = refresh });

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Refresh_WithGarbageToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = "totally-invalid-token" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

file sealed record TokenPair(string AccessToken, string RefreshToken, int ExpiresIn);

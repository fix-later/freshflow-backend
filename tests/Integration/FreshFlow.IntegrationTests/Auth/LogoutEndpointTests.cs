using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.IntegrationTests.Infrastructure;

namespace FreshFlow.IntegrationTests.Auth;

[Trait("Category", "Integration")]
public sealed class LogoutEndpointTests(AuthWebAppFactory factory)
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
    public async Task Logout_WithValidToken_Returns204()
    {
        var (access, refresh) = await LoginAsAdminAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);

        var response = await _client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = refresh });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Logout_WithoutBearerToken_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsJsonAsync("/api/v1/auth/logout",
            new { refreshToken = "some-token" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithAlreadyRevokedToken_StillReturns204()
    {
        var (access, refresh) = await LoginAsAdminAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);

        // First logout
        await _client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = refresh });

        // Second logout with same token — should still succeed (idempotent)
        var response = await _client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = refresh });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}

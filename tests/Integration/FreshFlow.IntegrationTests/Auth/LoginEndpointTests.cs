using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.IntegrationTests.Infrastructure;

namespace FreshFlow.IntegrationTests.Auth;

[Trait("Category", "Integration")]
public sealed class LoginEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Login_WithValidAdminCredentials_Returns200WithTokens()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@test.freshflow",
            password = "AdminP@ss1"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        body!.AccessToken.Should().NotBeNullOrEmpty();
        body.RefreshToken.Should().NotBeNullOrEmpty();
        body.ExpiresIn.Should().Be(900);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@test.freshflow",
            password = "WrongPassword1!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Login_WithMissingEmail_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "",
            password = "P@ss1"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_WithUnknownUser_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "nobody@nowhere.com",
            password = "P@ss1"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

file sealed record LoginResponse(string AccessToken, string RefreshToken, int ExpiresIn, object User);

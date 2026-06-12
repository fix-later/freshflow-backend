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
            identifier = "admin@test.freshflow",
            password = "AdminP@ss1"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await response.Content.ReadFromJsonAsync<Envelope<TokenBody>>();
        env!.Success.Should().BeTrue();
        env.Data!.AccessToken.Should().NotBeNullOrEmpty();
        env.Data.RefreshToken.Should().NotBeNullOrEmpty();
        env.Data.ExpiresIn.Should().Be(900);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            identifier = "admin@test.freshflow",
            password = "WrongPassword1!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var env = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        env!.Success.Should().BeFalse();
        env.Error!.Code.Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Login_WithMissingIdentifier_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            identifier = "",
            password = "P@ss1"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_WithUnknownUser_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            identifier = "nobody@nowhere.com",
            password = "P@ss1"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

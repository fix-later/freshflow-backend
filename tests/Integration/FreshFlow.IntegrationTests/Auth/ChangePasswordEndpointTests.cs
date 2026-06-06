using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.IntegrationTests.Infrastructure;

namespace FreshFlow.IntegrationTests.Auth;

[Trait("Category", "Integration")]
public sealed class ChangePasswordEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ChangePassword_WithValidCurrentPassword_Returns204AndAllowsLoginWithNewPassword()
    {
        var email = await CreateDriverAsync("OldP@ss1");
        var tokens = await LoginAsync(email, "OldP@ss1");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await _client.PostAsJsonAsync("/api/v1/auth/change-password", new
        {
            currentPassword = "OldP@ss1",
            newPassword = "NewP@ss1"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        _client.DefaultRequestHeaders.Authorization = null;
        var oldLogin = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            identifier = email,
            password = "OldP@ss1"
        });
        oldLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var newLogin = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            identifier = email,
            password = "NewP@ss1"
        });
        newLogin.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_RevokesExistingRefreshToken()
    {
        var email = await CreateDriverAsync("OldP@ss1");
        var tokens = await LoginAsync(email, "OldP@ss1");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await _client.PostAsJsonAsync("/api/v1/auth/change-password", new
        {
            currentPassword = "OldP@ss1",
            newPassword = "NewP@ss1"
        });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        _client.DefaultRequestHeaders.Authorization = null;
        var refresh = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = tokens.RefreshToken
        });

        refresh.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_Returns401AndKeepsOldPassword()
    {
        var email = await CreateDriverAsync("OldP@ss1");
        var tokens = await LoginAsync(email, "OldP@ss1");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await _client.PostAsJsonAsync("/api/v1/auth/change-password", new
        {
            currentPassword = "WrongP@ss1",
            newPassword = "NewP@ss1"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadFromJsonAsync<ChangePasswordErrorBody>();
        body!.Code.Should().Be("INVALID_CURRENT_PASSWORD");

        _client.DefaultRequestHeaders.Authorization = null;
        var oldLogin = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            identifier = email,
            password = "OldP@ss1"
        });
        oldLogin.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_WithWeakNewPassword_Returns400()
    {
        var email = await CreateDriverAsync("OldP@ss1");
        var tokens = await LoginAsync(email, "OldP@ss1");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await _client.PostAsJsonAsync("/api/v1/auth/change-password", new
        {
            currentPassword = "OldP@ss1",
            newPassword = "weak"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("VALIDATION_ERROR");
    }

    [Fact]
    public async Task ChangePassword_WithoutBearerToken_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.PostAsJsonAsync("/api/v1/auth/change-password", new
        {
            currentPassword = "OldP@ss1",
            newPassword = "NewP@ss1"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<string> CreateDriverAsync(string password)
    {
        var adminTokens = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminTokens.AccessToken);

        var email = $"change-password-{Guid.NewGuid():N}@test.freshflow";
        var response = await _client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email,
            password,
            role = "driver"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return email;
    }

    private async Task<(string AccessToken, string RefreshToken)> LoginAsync(string identifier, string password)
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            identifier,
            password
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ChangePasswordTokenPair>();
        return (body!.AccessToken, body.RefreshToken);
    }
}

file sealed record ChangePasswordTokenPair(string AccessToken, string RefreshToken, int ExpiresIn);
file sealed record ChangePasswordErrorBody(string Code, string Message);

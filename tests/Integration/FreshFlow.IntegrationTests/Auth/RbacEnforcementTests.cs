using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.IntegrationTests.Infrastructure;

namespace FreshFlow.IntegrationTests.Auth;

[Trait("Category", "Integration")]
public sealed class RbacEnforcementTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("GET", "/api/v1/admin/users")]
    [InlineData("POST", "/api/v1/admin/users")]
    public async Task AdminEndpoints_Unauthenticated_Return401(string method, string path)
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST")
            request.Content = JsonContent.Create(new { });

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminEndpoints_WithDriverJwt_Returns403()
    {
        // Create driver user via admin
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var createResp = await _client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email = "rbacdriver@test.freshflow",
            password = "DriverP@ss1",
            role = "driver"
        });
        createResp.EnsureSuccessStatusCode();

        var driverToken = await LoginAsync("rbacdriver@test.freshflow", "DriverP@ss1");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", driverToken);

        var response = await _client.GetAsync("/api/v1/admin/users");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var body = await response.Content.ReadFromJsonAsync<RbacErrorBody>();
        body.Should().NotBeNull();
        body!.Code.Should().Be("FORBIDDEN");
        body.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AuthLogout_WithoutBearer_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = "x" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<string> LoginAsync(string email, string password)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<RbacTokenPair>();
        return body!.AccessToken;
    }
}

file sealed record RbacTokenPair(string AccessToken, string RefreshToken, int ExpiresIn);
file sealed record RbacErrorBody(string Code, string Message);

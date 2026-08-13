using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using FreshFlow.IntegrationTests.Infrastructure;

namespace FreshFlow.IntegrationTests.Auth;

[Trait("Category", "Integration")]
public sealed class AdminUsersEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<string> LoginAsAdminAsync()
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            identifier = "admin@test.freshflow",
            password = "AdminP@ss1"
        });
        resp.EnsureSuccessStatusCode();
        var env = await resp.Content.ReadFromJsonAsync<Envelope<TokenBody>>();
        return env!.Data!.AccessToken;
    }

    [Fact]
    public async Task CreateUser_AsAdmin_Returns201()
    {
        var token = await LoginAsAdminAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email = "driver@test.freshflow",
            password = "DriverP@ss1",
            role = "driver"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateUser_WithoutAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email = "u@test.com",
            password = "P@ss1",
            role = "driver"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateUser_DuplicateEmail_Returns409()
    {
        var token = await LoginAsAdminAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await _client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email = "dup@test.freshflow",
            password = "DupP@ss1",
            role = "driver"
        });

        var response = await _client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email = "dup@test.freshflow",
            password = "DupP@ss1",
            role = "driver"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetUsers_AsAdmin_Returns200WithPagination()
    {
        var token = await LoginAsAdminAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/v1/admin/users?page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreatedUser_WithPhone_CanLoginUsingPhone()
    {
        var token = await LoginAsAdminAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var unique = Guid.NewGuid().ToString("N")[..8];
        var phone = $"+8490{Random.Shared.Next(1000000, 9999999)}";

        var create = await _client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email = $"phone-login-{unique}@test.freshflow",
            password = "DriverP@ss1",
            role = "driver",
            phone
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        _client.DefaultRequestHeaders.Authorization = null;
        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            identifier = phone,
            password = "DriverP@ss1"
        });

        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await login.Content.ReadFromJsonAsync<Envelope<TokenBody>>();
        env!.Data!.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateSearchAndLogin_WithFullName_ReturnsNormalizedFullName()
    {
        var token = await LoginAsAdminAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var unique = Guid.NewGuid().ToString("N")[..8];
        var email = string.Concat("full-name-", unique, "@test.freshflow");
        var fullName = string.Concat("Fresh Flow Driver ", unique);

        var create = await _client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email,
            password = "DriverP@ss1",
            role = "driver",
            fullName = string.Concat("  ", fullName, "  ")
        });

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var userId = created.GetProperty("data").GetProperty("id").GetGuid();
        created.GetProperty("data").GetProperty("fullName").GetString().Should().Be(fullName);

        var search = await _client.GetAsync(string.Concat(
            "/api/v1/admin/users?search=", Uri.EscapeDataString(fullName.ToUpperInvariant()),
            "&page=1&pageSize=10"));

        search.StatusCode.Should().Be(HttpStatusCode.OK);
        var searchBody = await search.Content.ReadFromJsonAsync<JsonElement>();
        var listedUser = searchBody.GetProperty("data").GetProperty("data")
            .EnumerateArray().Single(user => user.GetProperty("id").GetGuid() == userId);
        listedUser.GetProperty("fullName").GetString().Should().Be(fullName);

        _client.DefaultRequestHeaders.Authorization = null;
        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            identifier = email,
            password = "DriverP@ss1"
        });

        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginBody = await login.Content.ReadFromJsonAsync<JsonElement>();
        loginBody.GetProperty("data").GetProperty("user")
            .GetProperty("fullName").GetString().Should().Be(fullName);
    }
}

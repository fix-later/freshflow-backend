using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.IntegrationTests.Infrastructure;

namespace FreshFlow.IntegrationTests.Catalog;

/// <summary>
/// ASSIST-E2-T1 — verifies that GET /api/v1/markets carries no role/agent gate beyond
/// <c>[Authorize]</c> (any authenticated user), confirming the foundational assumption of
/// decision 2-A: the AI assistant's market picker can reuse <c>GetMarketsQuery</c> as-is via
/// this endpoint, with no additional authorization adapter needed.
/// </summary>
[Trait("Category", "Integration")]
public sealed class GetMarketsAccessTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetMarkets_Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/api/v1/markets");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMarkets_AsAdmin_Returns200()
    {
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.GetAsync("/api/v1/markets");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetMarkets_AsDriver_Returns200_NoRoleGateBeyondAuthenticated()
    {
        // "driver" carries none of the existing role/agent gates on this controller
        // (admin, market_agent) — if this 200s, GET /api/v1/markets is confirmed open
        // to every authenticated role, not just the ones this controller otherwise checks.
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var email = $"assist-e2-t1-driver-{Guid.NewGuid():N}@test.freshflow";
        var createResp = await _client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email,
            password = "DriverP@ss1",
            role = "driver"
        });
        createResp.EnsureSuccessStatusCode();

        var driverToken = await LoginAsync(email, "DriverP@ss1");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", driverToken);

        var response = await _client.GetAsync("/api/v1/markets");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<string> LoginAsync(string identifier, string password)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/auth/login", new { identifier, password });
        resp.EnsureSuccessStatusCode();
        var env = await resp.Content.ReadFromJsonAsync<Envelope<TokenBody>>();
        return env!.Data!.AccessToken;
    }
}

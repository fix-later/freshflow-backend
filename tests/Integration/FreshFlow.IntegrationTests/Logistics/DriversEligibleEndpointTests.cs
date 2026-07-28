using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Logistics.Application.Abstractions;

namespace FreshFlow.IntegrationTests.Logistics;

/// <summary>
/// GET /api/v1/hubs/{hubId}/drivers/eligible. The seam (<c>DriverRow</c>, ToSqlQuery) never runs
/// on the EF InMemory provider, so the role/active filter is only proven here against real Postgres.
/// </summary>
[Trait("Category", "Integration")]
public sealed class DriversEligibleEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ListEligible_ReturnsOnlyActiveDrivers_ExcludesOtherRolesAsync()
    {
        await AuthenticateAsAdminAsync();
        var driverEmail = $"eligible-driver-{Guid.NewGuid():N}@test.freshflow";
        var hubStaffEmail = $"eligible-hubstaff-{Guid.NewGuid():N}@test.freshflow";
        await CreateUserAsync(driverEmail, "driver");
        await CreateUserAsync(hubStaffEmail, "hub_staff");

        var response = await _client.GetAsync($"/api/v1/hubs/{Guid.NewGuid()}/drivers/eligible");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Envelope<IReadOnlyList<DriverDto>>>();
        body!.Data!.Should().Contain(d => d.RoleName == "driver" && d.IsActive);
        body.Data!.Should().OnlyContain(d => d.RoleName == "driver");
    }

    private async Task AuthenticateAsAdminAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { identifier = "admin@test.freshflow", password = "AdminP@ss1" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<TokenBody>>();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.Data!.AccessToken);
    }

    private async Task CreateUserAsync(string email, string role)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/admin/users",
            new { email, password = "EligibleP@ss1", role });
        response.EnsureSuccessStatusCode();
    }
}

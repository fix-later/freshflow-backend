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
    public async Task ListEligible_IsHubScopedActiveAndIncludesDisplayFieldsAsync()
    {
        await AuthenticateAsAdminAsync();
        var hubA = await CreateHubAsync("A");
        var hubB = await CreateHubAsync("B");
        var driverA = await CreateUserAsync("A", "Driver Alpha");
        var driverB = await CreateUserAsync("B", "Driver Beta");
        var inactive = await CreateUserAsync("inactive", "Inactive Driver");
        await ReplaceAssignmentsAsync(hubA, [driverA.Id, inactive.Id]);
        await ReplaceAssignmentsAsync(hubB, [driverB.Id]);
        await SetActiveAsync(inactive.Id, false);

        var response = await _client.GetAsync($"/api/v1/hubs/{hubA}/drivers/eligible");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Envelope<IReadOnlyList<DriverDto>>>();
        body!.Data.Should().ContainSingle().Which.Should().BeEquivalentTo(new DriverDto(
            driverA.Id, driverA.FullName, driverA.Email, "driver", true));
        body.Data.Should().NotContain(d => d.UserId == driverB.Id || d.UserId == inactive.Id);
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

    private async Task<TestUser> CreateUserAsync(string suffix, string fullName)
    {
        var email = $"eligible-{suffix}-{Guid.NewGuid():N}@test.freshflow";
        var response = await _client.PostAsJsonAsync(
            "/api/v1/admin/users",
            new { email, password = "EligibleP@ss1", role = "driver", fullName });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<CreateUserBody>>();
        return new TestUser(body!.Data!.Id, body.Data.Email, body.Data.FullName!);
    }

    private async Task<Guid> CreateHubAsync(string suffix)
    {
        var market = await _client.PostAsJsonAsync("/api/v1/markets", new
        {
            name = $"Eligible Market {suffix} {Guid.NewGuid():N}",
            location = $"Zone {suffix}",
            address = $"Market {suffix}"
        });
        market.EnsureSuccessStatusCode();
        var marketBody = await market.Content.ReadFromJsonAsync<Envelope<IdBody>>();

        var hub = await _client.PostAsJsonAsync("/api/v1/hubs", new
        {
            marketId = marketBody!.Data!.Id,
            name = $"Eligible Hub {suffix} {Guid.NewGuid():N}",
            address = $"Zone {suffix}",
            capacityKg = 1000m
        });
        hub.EnsureSuccessStatusCode();
        var hubBody = await hub.Content.ReadFromJsonAsync<Envelope<HubBody>>();
        return hubBody!.Data!.HubId;
    }

    private async Task ReplaceAssignmentsAsync(Guid hubId, IReadOnlyList<Guid> driverUserIds)
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/v1/hubs/{hubId}/driver-assignments", new { driverUserIds });
        response.EnsureSuccessStatusCode();
    }

    private async Task SetActiveAsync(Guid userId, bool isActive)
    {
        var response = await _client.PatchAsJsonAsync(
            $"/api/v1/admin/users/{userId}/activate", new { isActive });
        response.EnsureSuccessStatusCode();
    }

    private sealed record TestUser(Guid Id, string Email, string FullName);
    private sealed record CreateUserBody(Guid Id, string Email, string? FullName);
    private sealed record IdBody(Guid Id);
    private sealed record HubBody(Guid HubId);
}

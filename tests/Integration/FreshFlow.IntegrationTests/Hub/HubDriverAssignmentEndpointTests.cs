using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.IntegrationTests.Infrastructure;

namespace FreshFlow.IntegrationTests.Hub;

[Trait("Category", "Integration")]
public sealed class HubDriverAssignmentEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task DriverAssignments_ReplaceGetIsolateAndRejectIneligibleAsync()
    {
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        Authenticate(adminToken);
        var driverA = await CreateUserAsync("drv-a", "driver", "DriverP@ss1");
        var driverBoth = await CreateUserAsync("drv-both", "driver", "DriverP@ss1");
        var notDriver = await CreateUserAsync("staff", "hub_staff", "HubStaffP@ss1");
        var hubA = await CreateHubAsync("A");
        var hubB = await CreateHubAsync("B");

        // Null list fails validation before any write.
        var invalidReplace = await _client.PutAsJsonAsync(
            $"/api/v1/hubs/{hubA}/driver-assignments",
            new { driverUserIds = (Guid[]?)null });
        invalidReplace.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await invalidReplace.Content.ReadFromJsonAsync<ErrorEnvelope>())!
            .Error!.Code.Should().Be("VALIDATION_ERROR");

        // A non-driver role is an ineligible target (422), leaves roster untouched.
        var ineligible = await _client.PutAsJsonAsync(
            $"/api/v1/hubs/{hubA}/driver-assignments",
            new { driverUserIds = new[] { notDriver.Id } });
        ineligible.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ineligible.Content.ReadFromJsonAsync<ErrorEnvelope>())!
            .Error!.Code.Should().Be("INVALID_ASSIGNMENT_TARGET");

        // Unknown user id is not found (404).
        (await _client.PutAsJsonAsync(
            $"/api/v1/hubs/{hubA}/driver-assignments",
            new { driverUserIds = new[] { Guid.NewGuid() } }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Missing hub is not found (404).
        (await _client.GetAsync($"/api/v1/hubs/{Guid.NewGuid()}/driver-assignments"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Replace roundtrip on both hubs, then read back.
        await ReplaceAsync(hubA, [driverA.Id, driverBoth.Id]);
        await ReplaceAsync(hubB, [driverBoth.Id]);

        (await GetAssignmentsAsync(hubA)).Should().BeEquivalentTo([driverA.Id, driverBoth.Id]);
        (await GetAssignmentsAsync(hubB)).Should().Equal(driverBoth.Id);

        // Clearing one hub does not touch the other.
        await ReplaceAsync(hubA, []);
        (await GetAssignmentsAsync(hubA)).Should().BeEmpty();
        (await GetAssignmentsAsync(hubB)).Should().Equal(driverBoth.Id);
    }

    private async Task<TestUser> CreateUserAsync(string suffix, string role, string password)
    {
        var email = $"hub-{suffix}-{Guid.NewGuid():N}@test.freshflow";
        var response = await _client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email,
            password,
            role
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<CreateUserBody>>();
        return new TestUser(body!.Data!.Id, email, password);
    }

    private async Task<Guid> CreateHubAsync(string suffix)
    {
        var marketId = await CreateMarketAsync(suffix);
        var response = await _client.PostAsJsonAsync("/api/v1/hubs", new
        {
            marketId,
            name = $"Integration Hub {suffix} {Guid.NewGuid():N}",
            address = $"Zone {suffix}",
            latitude = (decimal?)null,
            longitude = (decimal?)null,
            capacityKg = 1000m,
            managedBy = (Guid?)null
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<HubBody>>();
        return body!.Data!.HubId;
    }

    private async Task<Guid> CreateMarketAsync(string suffix)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/markets", new
        {
            name = $"Integration Market {suffix} {Guid.NewGuid():N}",
            location = $"Zone {suffix}",
            address = $"Market {suffix}",
            latitude = (decimal?)null,
            longitude = (decimal?)null
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<IdBody>>();
        return body!.Data!.Id;
    }

    private async Task ReplaceAsync(Guid hubId, IReadOnlyList<Guid> userIds)
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/v1/hubs/{hubId}/driver-assignments",
            new { driverUserIds = userIds });
        response.EnsureSuccessStatusCode();
    }

    private async Task<IReadOnlyList<Guid>> GetAssignmentsAsync(Guid hubId)
    {
        var body = await _client.GetFromJsonAsync<Envelope<AssignmentsBody>>(
            $"/api/v1/hubs/{hubId}/driver-assignments");
        return body!.Data!.DriverUserIds;
    }

    private async Task<string> LoginAsync(string identifier, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            identifier,
            password
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<TokenBody>>();
        return body!.Data!.AccessToken;
    }

    private void Authenticate(string token) =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private sealed record TestUser(Guid Id, string Email, string Password);
    private sealed record CreateUserBody(Guid Id);
    private sealed record IdBody(Guid Id);
    private sealed record AssignmentsBody(Guid HubId, IReadOnlyList<Guid> DriverUserIds);
    private sealed record HubBody(Guid HubId, string Name, bool IsActive);
}

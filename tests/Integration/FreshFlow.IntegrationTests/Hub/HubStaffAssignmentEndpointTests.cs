using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.IntegrationTests.Infrastructure;

namespace FreshFlow.IntegrationTests.Hub;

[Trait("Category", "Integration")]
public sealed class HubStaffAssignmentEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task AssignmentLifecycle_EnforcesIsolationReplaceClearAndInactiveHubAsync()
    {
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        Authenticate(adminToken);
        var staffA = await CreateStaffAsync("a");
        var staffB = await CreateStaffAsync("b");
        var staffBoth = await CreateStaffAsync("both");
        var staleAdmin = await CreateUserAsync("stale-admin", "driver", "StaleAdminP@ss1");
        await SetRoleAsync(staleAdmin.Id, "admin");
        var hubA = await CreateHubAsync("A");
        var hubB = await CreateHubAsync("B");
        var staffAToken = await LoginAsync(staffA.Email, staffA.Password);
        var staffBToken = await LoginAsync(staffB.Email, staffB.Password);
        var staffBothToken = await LoginAsync(staffBoth.Email, staffBoth.Password);
        var staleAdminToken = await LoginAsync(staleAdmin.Email, staleAdmin.Password);

        Authenticate(staffAToken);
        var strictDenied = await _client.GetAsync(
            $"/api/v1/hubs/{hubA}/pending-inbound");
        strictDenied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await strictDenied.Content.ReadFromJsonAsync<ErrorEnvelope>())!
            .Error!.Code.Should().Be("HUB_ACCESS_DENIED");
        (await _client.GetAsync(
            $"/api/v1/hubs/{hubA}/pending-inbound?page_size=0"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        Authenticate(adminToken);
        (await _client.GetAsync($"/api/v1/hubs/{hubA}/pending-inbound"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var invalidReplace = await _client.PutAsJsonAsync(
            $"/api/v1/hubs/{hubA}/staff-assignments",
            new { staffUserIds = (Guid[]?)null });
        invalidReplace.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await invalidReplace.Content.ReadFromJsonAsync<ErrorEnvelope>())!
            .Error!.Code.Should().Be("VALIDATION_ERROR");

        await SetActiveAsync(staleAdmin.Id, false);
        Authenticate(staleAdminToken);
        (await _client.GetAsync($"/api/v1/hubs/{hubA}/pending-inbound"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.GetAsync($"/api/v1/hubs/{hubA}/staff-assignments"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.PutAsJsonAsync(
            $"/api/v1/hubs/{hubA}/staff-assignments",
            new { staffUserIds = Array.Empty<Guid>() }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        Authenticate(adminToken);
        await ReplaceAsync(hubA, [staffA.Id, staffBoth.Id]);
        await ReplaceAsync(hubB, [staffB.Id, staffBoth.Id]);

        var hubAAssignments = await _client
            .GetFromJsonAsync<Envelope<AssignmentsBody>>(
                $"/api/v1/hubs/{hubA}/staff-assignments");
        hubAAssignments!.Data!.StaffUserIds.Should()
            .BeEquivalentTo([staffA.Id, staffBoth.Id]);

        Authenticate(staffAToken);
        (await GetAssignedAsync()).Should().ContainSingle(hub => hub.HubId == hubA);
        (await _client.GetAsync($"/api/v1/hubs/{hubA}/pending-inbound"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.GetAsync($"/api/v1/hubs/{hubB}/pending-inbound"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.GetAsync(
            $"/api/v1/hubs/{hubA}/orders-by-restaurant?service_date=2026-07-29"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.GetAsync(
            $"/api/v1/hubs/{hubB}/orders-by-restaurant?service_date=2026-07-29"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.GetAsync(
            $"/api/v1/hubs/{Guid.NewGuid()}/orders-by-restaurant?service_date=2026-07-29"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        Authenticate(adminToken);
        await SetActiveAsync(staffA.Id, false);
        Authenticate(staffAToken);
        (await _client.GetAsync($"/api/v1/hubs/{hubA}/pending-inbound"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.GetAsync("/api/v1/hubs/assigned"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        Authenticate(adminToken);
        await SetActiveAsync(staffA.Id, true);

        Authenticate(staffBothToken);
        (await GetAssignedAsync()).Select(hub => hub.HubId)
            .Should().BeEquivalentTo([hubA, hubB]);

        Authenticate(adminToken);
        await ReplaceAsync(hubA, [staffB.Id]);
        await ReplaceAsync(hubB, []);

        Authenticate(staffBToken);
        (await GetAssignedAsync()).Should().ContainSingle(hub => hub.HubId == hubA);
        Authenticate(staffBothToken);
        (await GetAssignedAsync()).Should().BeEmpty();

        Authenticate(adminToken);
        await ReplaceAsync(hubA, [staffA.Id]);
        var deactivate = await _client.DeleteAsync($"/api/v1/hubs/{hubA}");
        deactivate.EnsureSuccessStatusCode();

        Authenticate(staffAToken);
        (await GetAssignedAsync()).Should().BeEmpty();
        (await _client.GetAsync($"/api/v1/hubs/{hubA}/pending-inbound"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        Authenticate(adminToken);
        var retained = await _client
            .GetFromJsonAsync<Envelope<AssignmentsBody>>(
                $"/api/v1/hubs/{hubA}/staff-assignments");
        retained!.Data!.StaffUserIds.Should().Equal(staffA.Id);
    }

    private Task<TestStaff> CreateStaffAsync(string suffix) =>
        CreateUserAsync(suffix, "hub_staff", "HubStaffP@ss1");

    private async Task<TestStaff> CreateUserAsync(
        string suffix,
        string role,
        string password)
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
        return new TestStaff(body!.Data!.Id, email, password);
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
            $"/api/v1/hubs/{hubId}/staff-assignments",
            new { staffUserIds = userIds });
        response.EnsureSuccessStatusCode();
    }

    private async Task SetActiveAsync(Guid userId, bool isActive)
    {
        var response = await _client.PatchAsJsonAsync(
            $"/api/v1/admin/users/{userId}/activate",
            new { isActive });
        response.EnsureSuccessStatusCode();
    }

    private async Task SetRoleAsync(Guid userId, string roleName)
    {
        var response = await _client.PatchAsJsonAsync(
            $"/api/v1/admin/users/{userId}/role",
            new { roleName });
        response.EnsureSuccessStatusCode();
    }

    private async Task<IReadOnlyList<HubBody>> GetAssignedAsync()
    {
        var response = await _client.GetAsync("/api/v1/hubs/assigned");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<List<HubBody>>>();
        return body!.Data!;
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

    private sealed record TestStaff(Guid Id, string Email, string Password);
    private sealed record CreateUserBody(Guid Id);
    private sealed record IdBody(Guid Id);
    private sealed record AssignmentsBody(Guid HubId, IReadOnlyList<Guid> StaffUserIds);
    private sealed record HubBody(Guid HubId, string Name, bool IsActive);
}

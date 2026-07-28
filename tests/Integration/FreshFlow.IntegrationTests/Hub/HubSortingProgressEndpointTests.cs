using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.IntegrationTests.Infrastructure;

namespace FreshFlow.IntegrationTests.Hub;

/// <summary>
/// POST/GET route-free sorting endpoints. Proves persistence + read-back on real Postgres,
/// and that the partial unique index on (hub_id, service_date, order_item_id) makes the POST
/// idempotent (two calls for the same line -> one row).
/// </summary>
[Trait("Category", "Integration")]
public sealed class HubSortingProgressEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static readonly DateOnly ServiceDate = new(2026, 7, 29);

    [Fact]
    public async Task MarkSorted_CalledTwiceForSameLine_UpsertsOneRowAsync()
    {
        await AuthenticateAsAdminAsync();
        var hubId = await CreateHubAsync();
        var orderItemId = Guid.NewGuid();

        var first = await _client.PostAsJsonAsync(
            $"/api/v1/hubs/{hubId}/sorting",
            new { serviceDate = ServiceDate, orderItemId, sortedQuantityKg = 4m });
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await _client.PostAsJsonAsync(
            $"/api/v1/hubs/{hubId}/sorting",
            new { serviceDate = ServiceDate, orderItemId, sortedQuantityKg = 9m });
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        var progress = await _client.GetFromJsonAsync<Envelope<List<SortingLineBody>>>(
            $"/api/v1/hubs/{hubId}/sorting-progress?serviceDate={ServiceDate:yyyy-MM-dd}");

        var line = progress!.Data!.Should().ContainSingle().Which;
        line.HubId.Should().Be(hubId);
        line.ServiceDate.Should().Be(ServiceDate);
        line.RouteId.Should().BeNull();
        line.OrderItemId.Should().Be(orderItemId);
        line.SortedQuantityKg.Should().Be(9m);
        line.Status.Should().Be("SORTED");
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

    private async Task<Guid> CreateHubAsync()
    {
        var marketResponse = await _client.PostAsJsonAsync("/api/v1/markets", new
        {
            name = $"Sorting Market {Guid.NewGuid():N}",
            location = "Zone S",
            address = "1 Sorting Street",
            latitude = (decimal?)null,
            longitude = (decimal?)null
        });
        marketResponse.EnsureSuccessStatusCode();
        var market = await marketResponse.Content.ReadFromJsonAsync<Envelope<IdBody>>();

        var hubResponse = await _client.PostAsJsonAsync("/api/v1/hubs", new
        {
            marketId = market!.Data!.Id,
            name = $"Sorting Hub {Guid.NewGuid():N}",
            address = "Zone S",
            latitude = (decimal?)null,
            longitude = (decimal?)null,
            capacityKg = 1000m,
            managedBy = (Guid?)null
        });
        hubResponse.EnsureSuccessStatusCode();
        var hub = await hubResponse.Content.ReadFromJsonAsync<Envelope<HubBody>>();
        return hub!.Data!.HubId;
    }

    private sealed record IdBody(Guid Id);
    private sealed record HubBody(Guid HubId);
    private sealed record SortingLineBody(
        Guid HubId,
        DateOnly ServiceDate,
        Guid? RouteId,
        Guid OrderItemId,
        decimal SortedQuantityKg,
        string Status);
}

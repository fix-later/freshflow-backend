using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using FreshFlow.IntegrationTests.Infrastructure;

namespace FreshFlow.IntegrationTests.Orders;

[Trait("Category", "Integration")]
public sealed class OrderingWindowEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetOrderingWindow_AsRestaurant_ReturnsOnlyPublicSettingsAsync()
    {
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var email = $"ordering-window-{Guid.NewGuid():N}@test.freshflow";
        const string password = "RestaurantP@ss1";
        var create = await _client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email,
            password,
            role = "restaurant",
            restaurantName = $"Ordering Window Restaurant {Guid.NewGuid():N}"
        });
        create.EnsureSuccessStatusCode();

        var restaurantToken = await LoginAsync(email, password);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", restaurantToken);

        var response = await _client.GetAsync("/api/v1/orders/ordering-window");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = document.RootElement.GetProperty("data");
        data.TryGetProperty("dailyCutoffTime", out _).Should().BeTrue();
        data.TryGetProperty("deliveryWindowDays", out _).Should().BeTrue();
        data.TryGetProperty("batchingEnabled", out _).Should().BeFalse();
        data.TryGetProperty("defaultRouteType", out _).Should().BeFalse();
    }

    private async Task<string> LoginAsync(string identifier, string password)
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login", new { identifier, password });
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<Envelope<TokenBody>>();
        return envelope!.Data!.AccessToken;
    }
}

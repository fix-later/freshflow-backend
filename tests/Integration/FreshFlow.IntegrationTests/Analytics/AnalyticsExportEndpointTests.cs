using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.IntegrationTests.Infrastructure;

namespace FreshFlow.IntegrationTests.Analytics;

[Trait("Category", "Integration")]
public sealed class AnalyticsExportEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private static readonly DateOnly From = new(2026, 7, 16);
    private static readonly DateOnly To = new(2026, 7, 16);
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("price-history", true)]
    [InlineData("order-history", false)]
    [InlineData("delivery-performance", false)]
    public async Task Export_EachDataset_ReturnsCsvAttachmentAsync(
        string dataset,
        bool includeMarketProductId)
    {
        await AuthenticateAsAdminAsync();

        var response = await _client.GetAsync(Endpoint(dataset, includeMarketProductId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var disposition = response.Content.Headers.ContentDisposition;
        disposition.Should().NotBeNull();
        disposition!.DispositionType.Should().Be("attachment");
        disposition.FileName.Should().NotBeNullOrWhiteSpace();
        disposition.ToString().Should().Contain("filename=\"");
    }

    [Fact]
    public async Task Export_DatasetInjection_Returns400_AndValidExportStillSucceedsAsync()
    {
        await AuthenticateAsAdminAsync();
        var injection = Uri.EscapeDataString("'; DROP TABLE orders;--");

        var invalid = await _client.GetAsync(
            $"/api/v1/analytics/export?dataset={injection}&from={From:yyyy-MM-dd}" +
            $"&to={To:yyyy-MM-dd}&format=csv");

        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await invalid.Content.ReadFromJsonAsync<ErrorEnvelope>();
        error!.Error!.Code.Should().Be("VALIDATION_ERROR");

        var valid = await _client.GetAsync(Endpoint("order-history", false));
        valid.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Export_WithRestaurantToken_Returns403Async()
    {
        await AuthenticateAsAdminAsync();
        var email = $"analytics-export-{Guid.NewGuid():N}@test.freshflow";
        const string password = "RestaurantP@ss1";
        var create = await _client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email,
            password,
            role = "restaurant",
            restaurantName = "Analytics Export RBAC",
        });
        create.EnsureSuccessStatusCode();
        var token = await LoginAsync(email, password);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync(Endpoint("order-history", false));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static string Endpoint(string dataset, bool includeMarketProductId)
    {
        var marketProduct = includeMarketProductId
            ? $"&marketProductId={Guid.NewGuid()}"
            : string.Empty;
        var format = dataset == "price-history" ? string.Empty : "&format=csv";
        return $"/api/v1/analytics/export?dataset={dataset}&from={From:yyyy-MM-dd}" +
            $"&to={To:yyyy-MM-dd}{format}{marketProduct}";
    }

    private async Task AuthenticateAsAdminAsync()
    {
        var token = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<string> LoginAsync(string identifier, string password)
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { identifier, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<TokenBody>>();
        return body!.Data!.AccessToken;
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Pricing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.IntegrationTests.Analytics;

[Trait("Category", "Integration")]
public sealed class AnalyticsPriceTrendEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task PriceTrends_ExecutesSeamSql_ComputesStatsAndUsesVietnamBucketsAsync()
    {
        await AuthenticateAsAdminAsync();
        var marketProduct = await CreateMarketProductAsync();
        await SeedSnapshotAsync(marketProduct.Id, 10m, new DateTime(2026, 7, 15, 17, 30, 0, DateTimeKind.Utc));
        await SeedSnapshotAsync(marketProduct.Id, 20m, new DateTime(2026, 7, 16, 3, 15, 0, DateTimeKind.Utc));
        await SeedSnapshotAsync(marketProduct.Id, 40m, new DateTime(2026, 7, 16, 3, 45, 0, DateTimeKind.Utc));
        await SeedSnapshotAsync(marketProduct.Id, 30m, new DateTime(2026, 7, 16, 16, 30, 0, DateTimeKind.Utc));
        await SeedSnapshotAsync(marketProduct.Id, 999m, new DateTime(2026, 7, 16, 17, 30, 0, DateTimeKind.Utc));

        var response = await _client.GetAsync(Endpoint(
            [marketProduct.Id],
            "2026-07-16",
            "2026-07-16",
            "hourly"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Envelope<PriceTrendsDto>>();
        var series = body!.Data!.Series.Should().ContainSingle().Subject;
        series.MarketProductId.Should().Be(marketProduct.Id);
        series.ProductName.Should().Be(marketProduct.ProductName);
        series.MarketName.Should().Be(marketProduct.MarketName);
        series.Interval.Should().Be("hourly");
        series.Summary.MinPrice.Should().Be(10m);
        series.Summary.MaxPrice.Should().Be(40m);
        series.Summary.AvgPrice.Should().Be(25m);
        series.Summary.PriceVolatility.Should().BeApproximately(
            (decimal)Math.Sqrt(500d / 3d),
            0.000001m);
        series.Points.Select(point => point.Timestamp.Hour).Should().Equal(0, 10, 23);
        series.Points.Should().OnlyContain(point => point.Timestamp.Offset == TimeSpan.FromHours(7));
        series.Points[1].Should().BeEquivalentTo(new
        {
            AvgPrice = 30m,
            MinPrice = 20m,
            MaxPrice = 40m,
            SnapshotCount = 2
        });
    }

    [Fact]
    public async Task PriceTrends_OnePointIsNull_AndSoftDeletedOrEmptyIsOmittedAsync()
    {
        await AuthenticateAsAdminAsync();
        var onePoint = await CreateMarketProductAsync();
        var softDeleted = await CreateMarketProductAsync();
        await SeedSnapshotAsync(onePoint.Id, 25m, new DateTime(2026, 7, 16, 5, 0, 0, DateTimeKind.Utc));
        await SeedSnapshotAsync(softDeleted.Id, 50m, new DateTime(2026, 7, 16, 6, 0, 0, DateTimeKind.Utc));
        await SoftDeleteMarketProductAsync(softDeleted.Id);

        var response = await _client.GetAsync(Endpoint(
            [onePoint.Id, softDeleted.Id],
            "2026-07-16",
            "2026-07-16",
            "daily"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Envelope<PriceTrendsDto>>();
        var series = body!.Data!.Series.Should().ContainSingle().Subject;
        series.MarketProductId.Should().Be(onePoint.Id);
        series.Summary.PriceVolatility.Should().BeNull();

        var emptyResponse = await _client.GetAsync(Endpoint(
            [Guid.NewGuid()],
            "2026-07-16",
            "2026-07-16",
            "daily"));
        emptyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var emptyBody = await emptyResponse.Content.ReadFromJsonAsync<Envelope<PriceTrendsDto>>();
        emptyBody!.Data!.Series.Should().BeEmpty();
    }

    [Fact]
    public async Task PriceTrends_RangeOverTwelveMonths_ForcesDailyAsync()
    {
        await AuthenticateAsAdminAsync();
        var marketProduct = await CreateMarketProductAsync();
        await SeedSnapshotAsync(marketProduct.Id, 10m, new DateTime(2025, 6, 1, 1, 0, 0, DateTimeKind.Utc));
        await SeedSnapshotAsync(marketProduct.Id, 20m, new DateTime(2025, 6, 1, 2, 0, 0, DateTimeKind.Utc));

        var response = await _client.GetAsync(Endpoint(
            [marketProduct.Id],
            "2025-01-01",
            "2026-01-02",
            "hourly"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Envelope<PriceTrendsDto>>();
        var series = body!.Data!.Series.Should().ContainSingle().Subject;
        series.Interval.Should().Be("daily");
        series.Points.Should().ContainSingle();
        series.Points.Single().AvgPrice.Should().Be(15m);
    }

    [Fact]
    public async Task PriceTrends_InvalidCountRangeAndInterval_Return400_WithoutDamagingTableAsync()
    {
        await AuthenticateAsAdminAsync();
        var marketProduct = await CreateMarketProductAsync();
        await SeedSnapshotAsync(marketProduct.Id, 10m, new DateTime(2026, 7, 16, 1, 0, 0, DateTimeKind.Utc));

        var elevenIds = Enumerable.Range(0, 11).Select(_ => Guid.NewGuid()).ToArray();
        var tooMany = await _client.GetAsync(Endpoint(elevenIds, "2026-07-01", "2026-07-16", "daily"));
        var reversed = await _client.GetAsync(Endpoint(
            [marketProduct.Id],
            "2026-07-16",
            "2026-07-01",
            "daily"));
        var overTwentyFourMonths = await _client.GetAsync(Endpoint(
            [marketProduct.Id],
            "2024-01-01",
            "2026-01-02",
            "daily"));
        var injection = await _client.GetAsync(Endpoint(
            [marketProduct.Id],
            "2026-07-01",
            "2026-07-16",
            "'; DROP TABLE price_snapshots;--"));

        tooMany.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        reversed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        overTwentyFourMonths.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        injection.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var validAfterInjection = await _client.GetAsync(Endpoint(
            [marketProduct.Id],
            "2026-07-16",
            "2026-07-16",
            "daily"));
        validAfterInjection.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PriceTrends_WithRestaurantToken_Returns200Async()
    {
        await AuthenticateAsAdminAsync();
        var marketProduct = await CreateMarketProductAsync();
        await SeedSnapshotAsync(marketProduct.Id, 10m, new DateTime(2026, 7, 16, 1, 0, 0, DateTimeKind.Utc));
        var restaurant = await CreateRestaurantAsync();
        var token = await LoginAsync(restaurant.Email, restaurant.Password);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync(Endpoint(
            [marketProduct.Id],
            "2026-07-16",
            "2026-07-16",
            "daily"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static string Endpoint(
        IReadOnlyList<Guid> marketProductIds,
        string from,
        string to,
        string interval)
    {
        var ids = string.Join("&", marketProductIds.Select(id => $"marketProductId={id}"));
        return $"/api/v1/analytics/price-trends?{ids}&from={from}&to={to}" +
            $"&interval={Uri.EscapeDataString(interval)}";
    }

    private async Task<MarketProductFixture> CreateMarketProductAsync()
    {
        var marketName = $"ANA-Market-{Guid.NewGuid():N}";
        var marketResponse = await _client.PostAsJsonAsync("/api/v1/markets", new
        {
            name = marketName,
            location = "Analytics Zone",
            address = "1 Analytics St",
            latitude = (decimal?)null,
            longitude = (decimal?)null
        });
        marketResponse.EnsureSuccessStatusCode();
        var market = await marketResponse.Content.ReadFromJsonAsync<Envelope<IdBody>>();

        var unitId = await GetOrCreateUnitAsync();
        var productName = $"ANA-Product-{Guid.NewGuid():N}";
        var productResponse = await _client.PostAsJsonAsync("/api/v1/products", new
        {
            name = productName,
            unitId,
            categoryId = (Guid?)null,
            description = (string?)null
        });
        productResponse.EnsureSuccessStatusCode();
        var product = await productResponse.Content.ReadFromJsonAsync<Envelope<IdBody>>();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var marketProduct = new MarketProduct(market!.Data!.Id, product!.Data!.Id, 10m, 1, null);
        db.Set<MarketProduct>().Add(marketProduct);
        await db.SaveChangesAsync();
        return new MarketProductFixture(marketProduct.Id, productName, marketName);
    }

    private async Task<Guid> GetOrCreateUnitAsync()
    {
        var listResponse = await _client.GetAsync("/api/v1/units");
        listResponse.EnsureSuccessStatusCode();
        var units = await listResponse.Content.ReadFromJsonAsync<Envelope<List<UnitBody>>>();
        var existing = units!.Data!.FirstOrDefault(unit =>
            unit.Name.Equals("kg", StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing.Id;
        }

        var createResponse = await _client.PostAsJsonAsync("/api/v1/units", new { name = "kg" });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<Envelope<IdBody>>();
        return created!.Data!.Id;
    }

    private async Task SeedSnapshotAsync(Guid marketProductId, decimal price, DateTime recordedAt)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO price_snapshots
                ("Id", "MarketProductId", "Price", "Quantity", "RecordedAt")
            VALUES ({Guid.NewGuid()}, {marketProductId}, {price}, {1}, {recordedAt})
            """);
    }

    private async Task SoftDeleteMarketProductAsync(Guid marketProductId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE market_products SET "deleted_at" = now() WHERE "Id" = {marketProductId}
            """);
    }

    private async Task AuthenticateAsAdminAsync()
    {
        var token = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<RestaurantCredentials> CreateRestaurantAsync()
    {
        var email = $"analytics-price-{Guid.NewGuid():N}@test.freshflow";
        const string password = "RestaurantP@ss1";
        var response = await _client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email,
            password,
            role = "restaurant",
            restaurantName = "Analytics Price Restaurant"
        });
        response.EnsureSuccessStatusCode();
        return new RestaurantCredentials(email, password);
    }

    private async Task<string> LoginAsync(string identifier, string password)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { identifier, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<TokenBody>>();
        return body!.Data!.AccessToken;
    }

    private sealed record IdBody(Guid Id);
    private sealed record UnitBody(Guid Id, string Name);
    private sealed record MarketProductFixture(Guid Id, string ProductName, string MarketName);
    private sealed record RestaurantCredentials(string Email, string Password);
}

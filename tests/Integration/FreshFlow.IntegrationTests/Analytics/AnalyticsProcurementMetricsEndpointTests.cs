using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.IntegrationTests.Analytics;

[Trait("Category", "Integration")]
public sealed class AnalyticsProcurementMetricsEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private static readonly DateTime CreatedAt =
        new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ProcurementMetrics_ExecutesSeamsAndAppliesDateMarketSoftDeleteAndNullableRulesAsync()
    {
        await AuthenticateAsAdminAsync();
        var marketId = Guid.NewGuid();
        var otherMarketId = Guid.NewGuid();
        var fromBatchId = Guid.NewGuid();
        var toBatchId = Guid.NewGuid();
        var incompleteBatchId = Guid.NewGuid();
        var afterRangeBatchId = Guid.NewGuid();
        var otherMarketBatchId = Guid.NewGuid();
        var softDeletedBatchId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await InsertBatchAsync(
                db,
                fromBatchId,
                new DateOnly(2026, 7, 1),
                marketId,
                "Built",
                new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 7, 1, 9, 30, 0, DateTimeKind.Utc));
            await InsertBatchAsync(
                db,
                toBatchId,
                new DateOnly(2026, 7, 3),
                marketId,
                "HandedOff",
                new DateTime(2026, 7, 3, 10, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 7, 3, 12, 0, 0, DateTimeKind.Utc));
            await InsertBatchAsync(
                db,
                incompleteBatchId,
                new DateOnly(2026, 7, 2),
                marketId,
                "Purchasing",
                new DateTime(2026, 7, 2, 10, 0, 0, DateTimeKind.Utc),
                null);
            await InsertBatchAsync(
                db,
                afterRangeBatchId,
                new DateOnly(2026, 7, 4),
                marketId,
                "HandedOff",
                CreatedAt,
                CreatedAt.AddHours(10));
            await InsertBatchAsync(
                db,
                otherMarketBatchId,
                new DateOnly(2026, 7, 2),
                otherMarketId,
                "HandedOff",
                CreatedAt,
                CreatedAt.AddHours(10));
            await InsertBatchAsync(
                db,
                softDeletedBatchId,
                new DateOnly(2026, 7, 2),
                marketId,
                "HandedOff",
                CreatedAt,
                CreatedAt.AddHours(10),
                CreatedAt);

            await InsertItemAsync(db, fromBatchId, 100m, 2, 120m);
            await InsertItemAsync(db, fromBatchId, 100m, 1, null);
            await InsertItemAsync(db, toBatchId, 100m, null, null);
            await InsertItemAsync(db, toBatchId, 0m, 3, 150m);
            await InsertItemAsync(db, incompleteBatchId, 100m, 4, 90m);
            await InsertItemAsync(db, incompleteBatchId, 100m, 10, 999m, CreatedAt);
            await InsertItemAsync(db, afterRangeBatchId, 100m, 1, 999m);
            await InsertItemAsync(db, otherMarketBatchId, 100m, 1, 999m);
            await InsertItemAsync(db, softDeletedBatchId, 100m, 1, 999m);

            await InsertExceptionAsync(db, fromBatchId, "Unavailable");
            await InsertExceptionAsync(db, toBatchId, "Shortfall");
            await InsertExceptionAsync(db, incompleteBatchId, "Damaged", CreatedAt);
            await InsertExceptionAsync(db, afterRangeBatchId, "Damaged");
            await InsertExceptionAsync(db, otherMarketBatchId, "Damaged");
            await InsertExceptionAsync(db, softDeletedBatchId, "PriceDiscrepancy");
        }

        var response = await _client.GetAsync(Endpoint(
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 3),
            marketId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Envelope<ProcurementMetricsDto>>();
        body!.Data!.TotalBatches.Should().Be(3);
        body.Data.StatusCounts.Should().Contain(new Dictionary<string, int>
        {
            ["Built"] = 1,
            ["Manifested"] = 0,
            ["Purchasing"] = 1,
            ["HandedOff"] = 1
        });
        body.Data.CompletionRatePercent.Should().Be(33.33m);
        body.Data.ItemsTotal.Should().Be(5);
        body.Data.ItemsPurchased.Should().Be(4);
        body.Data.ItemsPending.Should().Be(1);
        body.Data.TotalActualCostVND.Should().Be(1_050m);
        body.Data.PriceVariancePercent.Should().Be(5m);
        body.Data.AvgLeadTimeMinutes.Should().Be(105m);
        body.Data.ExceptionCount.Should().Be(2);
        body.Data.ExceptionsByType.Should().Contain(new Dictionary<string, int>
        {
            ["Unavailable"] = 1,
            ["Shortfall"] = 1,
            ["PriceDiscrepancy"] = 0,
            ["Damaged"] = 0
        });
    }

    [Fact]
    public async Task ProcurementMetrics_WithRestaurantToken_Returns403Async()
    {
        await AuthenticateAsAdminAsync();
        var restaurant = await CreateRestaurantAsync();
        var token = await LoginAsync(restaurant.Email, restaurant.Password);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync(Endpoint(
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 3),
            Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static string Endpoint(DateOnly from, DateOnly to, Guid marketId) =>
        $"/api/v1/analytics/procurement-metrics?from={from:yyyy-MM-dd}" +
        $"&to={to:yyyy-MM-dd}&marketId={marketId}";

    private static Task InsertBatchAsync(
        AppDbContext db,
        Guid batchId,
        DateOnly batchDate,
        Guid marketId,
        string status,
        DateTime? manifestedAt,
        DateTime? handedOffAt,
        DateTime? deletedAt = null) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO procurement_batches
                (id, batch_date, market_id, status, total_item_count,
                 manifested_at, handed_off_at, created_at, updated_at, deleted_at)
            VALUES
                ({batchId}, {batchDate}, {marketId}, {status}, {0},
                 {manifestedAt}, {handedOffAt}, {CreatedAt}, {CreatedAt}, {deletedAt})
            """);

    private static Task InsertItemAsync(
        AppDbContext db,
        Guid batchId,
        decimal? referenceUnitPrice,
        int? actualQuantity,
        decimal? actualUnitPrice,
        DateTime? deletedAt = null)
    {
        var itemId = Guid.NewGuid();
        var marketProductId = Guid.NewGuid();
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO procurement_batch_items
                (id, procurement_batch_id, market_product_id, product_name_snapshot,
                 total_quantity, reference_unit_price, actual_quantity, actual_unit_price,
                 created_at, updated_at, deleted_at)
            VALUES
                ({itemId}, {batchId}, {marketProductId}, {"Analytics Item"},
                 {1}, {referenceUnitPrice}, {actualQuantity}, {actualUnitPrice},
                 {CreatedAt}, {CreatedAt}, {deletedAt})
            """);
    }

    private static Task InsertExceptionAsync(
        AppDbContext db,
        Guid batchId,
        string type,
        DateTime? deletedAt = null)
    {
        var exceptionId = Guid.NewGuid();
        var marketProductId = Guid.NewGuid();
        var reportedByUserId = Guid.NewGuid();
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO procurement_exceptions
                (id, procurement_batch_id, market_product_id, type, reported_quantity,
                 reported_by_user_id, reported_at, created_at, updated_at, deleted_at)
            VALUES
                ({exceptionId}, {batchId}, {marketProductId}, {type}, {1},
                 {reportedByUserId}, {CreatedAt}, {CreatedAt}, {CreatedAt}, {deletedAt})
            """);
    }

    private async Task AuthenticateAsAdminAsync()
    {
        var token = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<RestaurantCredentials> CreateRestaurantAsync()
    {
        var email = $"analytics-procurement-{Guid.NewGuid():N}@test.freshflow";
        const string password = "RestaurantP@ss1";
        var response = await _client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email,
            password,
            role = "restaurant",
            restaurantName = "Analytics Procurement Restaurant"
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

    private sealed record RestaurantCredentials(string Email, string Password);
}

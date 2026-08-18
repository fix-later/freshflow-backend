using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Orders.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.IntegrationTests.Analytics;

[Trait("Category", "Integration")]
public sealed class AnalyticsOrderMetricsEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task OrderMetrics_ExecutesSeamSql_FiltersAndUsesVietnamBucketsAsync()
    {
        await AuthenticateAsAdminAsync();
        var restaurant = await CreateRestaurantAsync();
        var otherRestaurant = await CreateRestaurantAsync();
        var at2330Vietnam = new DateTime(2026, 7, 16, 16, 30, 0, DateTimeKind.Utc);
        var nextVietnamDay = new DateTime(2026, 7, 16, 17, 30, 0, DateTimeKind.Utc);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var delivered = NewOrder(restaurant.RestaurantId);
            var cancelled = NewOrder(restaurant.RestaurantId);
            var draft = NewOrder(restaurant.RestaurantId);
            var softDeleted = NewOrder(restaurant.RestaurantId);
            var outOfRange = NewOrder(restaurant.RestaurantId);
            var otherOrder = NewOrder(otherRestaurant.RestaurantId);
            db.Set<Order>().AddRange(delivered, cancelled, draft, softDeleted, outOfRange, otherOrder);
            await db.SaveChangesAsync();

            await SetOrderAsync(db, delivered.Id, at2330Vietnam, 300m, "Delivered", null);
            await SetOrderAsync(db, cancelled.Id, at2330Vietnam, 500m, "Cancelled", null);
            await SetOrderAsync(db, draft.Id, at2330Vietnam, 700m, "Draft", null);
            await SetOrderAsync(db, softDeleted.Id, at2330Vietnam, 900m, "Confirmed", at2330Vietnam);
            await SetOrderAsync(db, outOfRange.Id, nextVietnamDay, 200m, "Confirmed", null);
            await SetOrderAsync(db, otherOrder.Id, at2330Vietnam, 1_000m, "Confirmed", null);
        }

        var response = await _client.GetAsync(Endpoint(
            "2026-07-16",
            "2026-07-16",
            restaurant.RestaurantId,
            "day"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Envelope<OrderMetricsDto>>();
        body!.Data!.Summary.TotalOrders.Should().Be(2);
        body.Data.Summary.TotalRevenueVND.Should().Be(300m);
        body.Data.Summary.AvgOrderValueVND.Should().Be(150m);
        body.Data.Summary.CancelledCount.Should().Be(1);
        body.Data.Summary.CancellationRatePercent.Should().Be(50m);
        body.Data.Summary.DeliveredCount.Should().Be(1);
        body.Data.Summary.StatusCounts.Should().HaveCount(8);
        // The draft order is a cart, not a placed order: the seam filters it out entirely.
        body.Data.Summary.StatusCounts["Draft"].Should().Be(0);
        body.Data.Summary.StatusCounts["Confirmed"].Should().Be(0);
        body.Data.Summary.StatusCounts["Delivered"].Should().Be(1);
        body.Data.Summary.StatusCounts["Cancelled"].Should().Be(1);
        body.Data.Buckets.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Date = new DateOnly(2026, 7, 16),
            OrderCount = 2,
            RevenueVND = 300m
        });

        var week = await ReadMetricsAsync(restaurant.RestaurantId, "week");
        week.Buckets.Should().ContainSingle().Which.Date.Should().Be(new DateOnly(2026, 7, 13));

        var month = await ReadMetricsAsync(restaurant.RestaurantId, "month");
        month.Buckets.Should().ContainSingle().Which.Date.Should().Be(new DateOnly(2026, 7, 1));

        var other = await ReadMetricsAsync(otherRestaurant.RestaurantId, "day");
        other.Summary.TotalOrders.Should().Be(1);
        other.Summary.TotalRevenueVND.Should().Be(1_000m);
    }

    [Fact]
    public async Task OrderMetrics_GroupByInjection_Returns400_AndOrdersTableSurvivesAsync()
    {
        await AuthenticateAsAdminAsync();
        var restaurant = await CreateRestaurantAsync();

        var injection = await _client.GetAsync(Endpoint(
            "2026-07-16",
            "2026-07-16",
            restaurant.RestaurantId,
            "'; DROP TABLE orders;--"));

        injection.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var valid = await _client.GetAsync(Endpoint(
            "2026-07-16",
            "2026-07-16",
            restaurant.RestaurantId,
            "day"));
        valid.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OrderMetrics_WithRestaurantToken_Returns403Async()
    {
        await AuthenticateAsAdminAsync();
        var restaurant = await CreateRestaurantAsync();
        var token = await LoginAsync(restaurant.Email, restaurant.Password);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync(Endpoint(
            "2026-07-16",
            "2026-07-16",
            restaurant.RestaurantId,
            "day"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<OrderMetricsDto> ReadMetricsAsync(Guid restaurantId, string groupBy)
    {
        var response = await _client.GetAsync(Endpoint(
            "2026-07-16",
            "2026-07-16",
            restaurantId,
            groupBy));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<OrderMetricsDto>>();
        return body!.Data!;
    }

    private static string Endpoint(
        string from,
        string to,
        Guid? restaurantId,
        string groupBy) =>
        $"/api/v1/analytics/order-metrics?from={from}&to={to}" +
        $"&restaurantId={restaurantId}&groupBy={Uri.EscapeDataString(groupBy)}";

    private static Order NewOrder(Guid restaurantId)
    {
        var order = new Order(restaurantId, null, null);
        order.ClearDomainEvents();
        return order;
    }

    private static Task SetOrderAsync(
        AppDbContext db,
        Guid orderId,
        DateTime createdAt,
        decimal totalAmount,
        string status,
        DateTime? deletedAt) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE orders
            SET "CreatedAt" = {createdAt},
                "TotalAmount" = {totalAmount},
                "Status" = {status},
                "deleted_at" = {deletedAt}
            WHERE "Id" = {orderId}
            """);

    private async Task AuthenticateAsAdminAsync()
    {
        var token = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<CreatedRestaurant> CreateRestaurantAsync()
    {
        var email = $"analytics-metrics-{Guid.NewGuid():N}@test.freshflow";
        const string password = "RestaurantP@ss1";
        var create = await _client.PostAsJsonAsync(
            "/api/v1/admin/users",
            new
            {
                email,
                password,
                role = "restaurant",
                restaurantName = "Analytics Metrics Restaurant"
            });
        create.EnsureSuccessStatusCode();

        var response = await _client.GetAsync(
            $"/api/v1/admin/users?search={Uri.EscapeDataString(email)}&page=1&pageSize=10");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<UserListBody>>();
        var restaurantId = body!.Data!.Data.Should().ContainSingle()
            .Which.RestaurantId.Should().NotBeNull().And.Subject!.Value;

        return new CreatedRestaurant(restaurantId, email, password);
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

    private sealed record CreatedRestaurant(Guid RestaurantId, string Email, string Password);
    private sealed record UserListBody(IReadOnlyList<UserSummaryBody> Data);
    private sealed record UserSummaryBody(Guid? RestaurantId);
}

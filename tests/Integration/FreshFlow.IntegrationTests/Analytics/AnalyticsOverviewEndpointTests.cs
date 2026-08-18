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
public sealed class AnalyticsOverviewEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Overview_ExecutesSeamSql_UsesVietnamDay_ExcludesSoftDeleted_AndDraftsAsync()
    {
        await AuthenticateAsAdminAsync();
        var restaurant = await CreateRestaurantAsync();
        var targetDate = new DateOnly(2026, 7, 16);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var included = NewOrder(restaurant.RestaurantId);
            var draftInRange = NewOrder(restaurant.RestaurantId);
            var nextVietnamDay = NewOrder(restaurant.RestaurantId);
            var softDeleted = NewOrder(restaurant.RestaurantId);
            db.Set<Order>().AddRange(included, draftInRange, nextVietnamDay, softDeleted);
            await db.SaveChangesAsync();

            var at2330Vietnam = new DateTime(2026, 7, 16, 16, 30, 0, DateTimeKind.Utc);
            var at0030NextVietnamDay = new DateTime(2026, 7, 16, 17, 30, 0, DateTimeKind.Utc);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE orders SET \"CreatedAt\" = {at2330Vietnam}, \"TotalAmount\" = {100m}, \"Status\" = 'Confirmed' WHERE \"Id\" = {included.Id}");
            // Drafts are carts: excluded from orders, revenue and pending alike.
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE orders SET \"CreatedAt\" = {at2330Vietnam}, \"TotalAmount\" = {500m} WHERE \"Id\" = {draftInRange.Id}");
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE orders SET \"CreatedAt\" = {at0030NextVietnamDay}, \"TotalAmount\" = {200m} WHERE \"Id\" = {nextVietnamDay.Id}");
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE orders SET \"CreatedAt\" = {at2330Vietnam}, \"TotalAmount\" = {300m}, \"deleted_at\" = {at2330Vietnam} WHERE \"Id\" = {softDeleted.Id}");
        }

        var response = await _client.GetAsync($"/api/v1/analytics/overview?date={targetDate:yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Envelope<DashboardOverviewDto>>();
        body.Should().NotBeNull();
        body!.Data.Should().NotBeNull();
        body.Data!.OrdersToday.Should().Be(1);
        body.Data.RevenueToday.Should().Be(100m);
        body.Data.PendingOrders.Should().Be(1);
        body.Data.CancelledToday.Should().Be(0);
        body.Data.ActiveProcurementBatches.Should().Be(0);
        body.Data.DeliveriesToday.Should().Be(0);
        body.Data.OnTimeRatePercent.Should().Be(0m);
        body.Data.HubInboundKgToday.Should().Be(0m);
        body.Data.HubOutboundKgToday.Should().Be(0m);
    }

    [Fact]
    public async Task Overview_WithRestaurantToken_Returns403Async()
    {
        await AuthenticateAsAdminAsync();
        var restaurant = await CreateRestaurantAsync();
        var token = await LoginAsync(restaurant.Email, restaurant.Password);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/v1/analytics/overview?date=2026-07-16");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static Order NewOrder(Guid restaurantId)
    {
        var order = new Order(restaurantId, null, null);
        order.ClearDomainEvents();
        return order;
    }

    private async Task AuthenticateAsAdminAsync()
    {
        var token = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<CreatedRestaurant> CreateRestaurantAsync()
    {
        var email = $"analytics-{Guid.NewGuid():N}@test.freshflow";
        const string password = "RestaurantP@ss1";
        var create = await _client.PostAsJsonAsync(
            "/api/v1/admin/users",
            new
            {
                email,
                password,
                role = "restaurant",
                restaurantName = "Analytics Test Restaurant"
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


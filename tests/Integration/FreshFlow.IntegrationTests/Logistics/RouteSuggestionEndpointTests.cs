using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.Catalog.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.Pricing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.IntegrationTests.Logistics;

[Trait("Category", "Integration")]
public sealed class RouteSuggestionEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private static readonly DateOnly ServiceDate = new(2026, 7, 29);
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Suggestions_FilterByServiceDate_AndOptionallyIncludeBatchedAsync()
    {
        await AuthenticateAsAdminAsync();
        var firstRestaurantId = await CreateRestaurantAsync("Suggestion Restaurant One");
        var secondRestaurantId = await CreateRestaurantAsync("Suggestion Restaurant Two");
        var marketId = await SeedAsync(firstRestaurantId, secondRestaurantId);

        var defaultResponse = await _client.GetAsync(
            $"/api/v1/logistics/routes/suggestions?service_date={ServiceDate:yyyy-MM-dd}");

        defaultResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var defaultBody = await defaultResponse.Content
            .ReadFromJsonAsync<Envelope<RouteSuggestionsDto>>();
        defaultBody!.Data!.ServiceDate.Should().Be(ServiceDate);
        defaultBody.Data.Markets.Should().ContainSingle()
            .Which.Should().Be(new SuggestionItemDto(marketId, "Suggestion Market", 2));
        defaultBody.Data.Restaurants.Should().BeEquivalentTo(
        [
            new SuggestionItemDto(firstRestaurantId, "Suggestion Restaurant One", 1),
            new SuggestionItemDto(secondRestaurantId, "Suggestion Restaurant Two", 1)
        ]);

        var batchedResponse = await _client.GetAsync(
            $"/api/v1/logistics/routes/suggestions?service_date={ServiceDate:yyyy-MM-dd}&include_batched=true");

        batchedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var batchedBody = await batchedResponse.Content
            .ReadFromJsonAsync<Envelope<RouteSuggestionsDto>>();
        batchedBody!.Data!.Markets.Should().ContainSingle()
            .Which.Should().Be(new SuggestionItemDto(marketId, "Suggestion Market", 3));
        batchedBody.Data.Restaurants.Should().BeEquivalentTo(
        [
            new SuggestionItemDto(firstRestaurantId, "Suggestion Restaurant One", 1),
            new SuggestionItemDto(secondRestaurantId, "Suggestion Restaurant Two", 2)
        ]);
    }

    private async Task<Guid> SeedAsync(Guid firstRestaurantId, Guid secondRestaurantId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await ActivateRestaurantAsync(db, firstRestaurantId, "1 Suggestion St");
        await ActivateRestaurantAsync(db, secondRestaurantId, "2 Suggestion St");

        var unit = new UnitOfMeasurement($"unit-{Guid.NewGuid():N}", "unit");
        var market = new Market("Suggestion Market", "HCMC", "3 Suggestion St", null, null);
        db.Set<UnitOfMeasurement>().Add(unit);
        db.Set<Market>().Add(market);
        await db.SaveChangesAsync();

        var product = new Product("Suggestion Product", unit.Id, null, null, null);
        db.Set<Product>().Add(product);
        await db.SaveChangesAsync();

        var marketProduct = new MarketProduct(market.Id, product.Id, 10_000m, 100, null);
        db.Set<MarketProduct>().Add(marketProduct);
        await db.SaveChangesAsync();

        var scheduledFor = ServiceDate.ToDateTime(new TimeOnly(10, 0), DateTimeKind.Utc);

        // Early-morning VN boundary: 2026-07-28 22:00 UTC == 2026-07-29 05:00 Asia/Ho_Chi_Minh.
        // It must still bucket into ServiceDate (07-29); a naive UTC-date filter would misplace it
        // on 07-28 and drop it — this order is the timezone regression guard.
        var earlyMorningVn = new DateTime(2026, 7, 28, 22, 0, 0, DateTimeKind.Utc);
        var orders = new[]
        {
            NewOrder(firstRestaurantId, earlyMorningVn, marketProduct.Id),
            NewOrder(secondRestaurantId, scheduledFor, marketProduct.Id),
            NewOrder(secondRestaurantId, scheduledFor, marketProduct.Id),
            NewOrder(firstRestaurantId, scheduledFor, marketProduct.Id),
            NewOrder(firstRestaurantId, scheduledFor.AddDays(1), marketProduct.Id)
        };
        db.Set<Order>().AddRange(orders);
        db.Entry(orders[0]).Property(nameof(Order.Status)).CurrentValue = OrderStatus.AtHub;
        db.Entry(orders[1]).Property(nameof(Order.Status)).CurrentValue = OrderStatus.AtHub;
        db.Entry(orders[2]).Property(nameof(Order.Status)).CurrentValue = OrderStatus.Batched;
        db.Entry(orders[4]).Property(nameof(Order.Status)).CurrentValue = OrderStatus.AtHub;
        await db.SaveChangesAsync();

        return market.Id;
    }

    private static Order NewOrder(Guid restaurantId, DateTime scheduledFor, Guid marketProductId)
    {
        var order = new Order(restaurantId, scheduledFor, null);
        order.AddItem(marketProductId, "Suggestion Product", 1, 10_000m)
            .IsSuccess.Should().BeTrue();
        order.ClearDomainEvents();
        return order;
    }

    private static async Task ActivateRestaurantAsync(
        AppDbContext db,
        Guid restaurantId,
        string address)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE restaurants SET status = 'active' WHERE \"Id\" = {restaurantId}");
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO delivery_addresses
                ("Id", "RestaurantId", "AddressLine", "Latitude", "Longitude",
                 "IsDefault", "CreatedAt", "UpdatedAt", "DeletedAt")
            VALUES
                ({Guid.NewGuid()}, {restaurantId}, {address}, {10.75m}, {106.67m},
                 {true}, {DateTime.UtcNow}, {DateTime.UtcNow}, {null})
            """);
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

    private async Task<Guid> CreateRestaurantAsync(string name)
    {
        var email = $"suggestion-{Guid.NewGuid():N}@test.freshflow";
        var create = await _client.PostAsJsonAsync(
            "/api/v1/admin/users",
            new
            {
                email,
                password = "RestaurantP@ss1",
                role = "restaurant",
                restaurantName = name
            });
        create.EnsureSuccessStatusCode();

        var response = await _client.GetAsync(
            $"/api/v1/admin/users?search={Uri.EscapeDataString(email)}&page=1&pageSize=10");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<UserListBody>>();
        return body!.Data!.Data.Should().ContainSingle()
            .Which.RestaurantId.Should().NotBeNull().And.Subject!.Value;
    }

    private sealed record UserListBody(IReadOnlyList<UserSummaryBody> Data);

    private sealed record UserSummaryBody(Guid? RestaurantId);
}

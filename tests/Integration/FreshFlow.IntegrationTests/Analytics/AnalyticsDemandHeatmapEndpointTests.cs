using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Pricing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.IntegrationTests.Analytics;

[Trait("Category", "Integration")]
public sealed class AnalyticsDemandHeatmapEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private static readonly DateOnly TargetDate = new(2026, 7, 16);
    private static readonly DateTime CreatedAt =
        new(2026, 7, 16, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime At2330Vietnam =
        new(2026, 7, 16, 16, 30, 0, DateTimeKind.Utc);
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task DemandEndpoints_ExecuteSeamsAndApplyVietnamTimeCategoriesAndExclusionsAsync()
    {
        await AuthenticateAsAdminAsync();
        var catalog = await CreateCatalogFixtureAsync();
        var primary = await CreateRestaurantAsync("Primary");
        var zeroOrders = await CreateRestaurantAsync("Zero");
        var noAddress = await CreateRestaurantAsync("NoAddress");
        var inactive = await CreateRestaurantAsync("Inactive");
        var deletedAddress = await CreateRestaurantAsync("DeletedAddress");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await SetRestaurantStatusAsync(db, primary.RestaurantId, "active");
            await SetRestaurantStatusAsync(db, zeroOrders.RestaurantId, "active");
            await SetRestaurantStatusAsync(db, noAddress.RestaurantId, "active");
            await SetRestaurantStatusAsync(db, inactive.RestaurantId, "pending");
            await SetRestaurantStatusAsync(db, deletedAddress.RestaurantId, "active");
            await InsertAddressAsync(db, primary.RestaurantId, 10.75m, 106.67m);
            await InsertAddressAsync(db, zeroOrders.RestaurantId, 10.76m, 106.68m);
            await InsertAddressAsync(db, inactive.RestaurantId, 10.77m, 106.69m);
            await InsertAddressAsync(
                db,
                deletedAddress.RestaurantId,
                10.78m,
                106.70m,
                CreatedAt);

            var confirmed = NewOrder(
                primary.RestaurantId,
                new OrderItemSeed(catalog.VegetableMarketProductId, "Vegetable", 5, 10m),
                new OrderItemSeed(catalog.FruitMarketProductId, "Fruit", 2, 10m));
            var delivered = NewOrder(
                primary.RestaurantId,
                new OrderItemSeed(catalog.FruitMarketProductId, "Fruit", 1, 30m),
                new OrderItemSeed(catalog.UncategorizedMarketProductId, "Other", 3, 10m));
            var draft = NewOrder(
                primary.RestaurantId,
                new OrderItemSeed(catalog.UncategorizedMarketProductId, "Other", 1, 500m));
            var cancelled = NewOrder(
                primary.RestaurantId,
                new OrderItemSeed(catalog.UncategorizedMarketProductId, "Other", 1, 700m));
            var softDeleted = NewOrder(
                primary.RestaurantId,
                new OrderItemSeed(catalog.FruitMarketProductId, "Fruit", 100, 10m));
            var nextVietnamDay = NewOrder(
                primary.RestaurantId,
                new OrderItemSeed(catalog.FruitMarketProductId, "Fruit", 100, 10m));
            var noAddressOrder = NewOrder(
                noAddress.RestaurantId,
                new OrderItemSeed(catalog.VegetableMarketProductId, "Vegetable", 1, 10m));
            var inactiveOrder = NewOrder(
                inactive.RestaurantId,
                new OrderItemSeed(catalog.VegetableMarketProductId, "Vegetable", 1, 10m));
            var deletedAddressOrder = NewOrder(
                deletedAddress.RestaurantId,
                new OrderItemSeed(catalog.VegetableMarketProductId, "Vegetable", 1, 10m));
            db.Set<Order>().AddRange(
                confirmed,
                delivered,
                draft,
                cancelled,
                softDeleted,
                nextVietnamDay,
                noAddressOrder,
                inactiveOrder,
                deletedAddressOrder);
            await db.SaveChangesAsync();

            await SetOrderAsync(db, confirmed.Id, At2330Vietnam, "Confirmed");
            await SetOrderAsync(db, delivered.Id, At2330Vietnam, "Delivered");
            await SetOrderAsync(db, draft.Id, At2330Vietnam, "Draft");
            await SetOrderAsync(db, cancelled.Id, At2330Vietnam, "Cancelled");
            await SetOrderAsync(db, softDeleted.Id, At2330Vietnam, "Confirmed", CreatedAt);
            await SetOrderAsync(
                db,
                nextVietnamDay.Id,
                At2330Vietnam.AddHours(1),
                "Confirmed");
            await SetOrderAsync(db, noAddressOrder.Id, At2330Vietnam, "Confirmed");
            await SetOrderAsync(db, inactiveOrder.Id, At2330Vietnam, "Confirmed");
            await SetOrderAsync(db, deletedAddressOrder.Id, At2330Vietnam, "Confirmed");
        }

        var heatmapResponse = await _client.GetAsync(HeatmapEndpoint());

        heatmapResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var heatmapBody = await heatmapResponse.Content
            .ReadFromJsonAsync<Envelope<List<DemandHeatmapPointDto>>>();
        var point = heatmapBody!.Data.Should().ContainSingle().Subject;
        point.RestaurantId.Should().Be(primary.RestaurantId);
        point.RestaurantName.Should().Be(primary.RestaurantName);
        point.Latitude.Should().Be(10.75m);
        point.Longitude.Should().Be(106.67m);
        point.TotalOrderCount.Should().Be(4);
        point.TotalOrderValueVND.Should().Be(130m);
        point.DominantProductCategory.Should().Be(catalog.VegetableCategoryName);
        heatmapBody.Data.Should().NotContain(row =>
            row.RestaurantId == zeroOrders.RestaurantId ||
            row.RestaurantId == noAddress.RestaurantId ||
            row.RestaurantId == inactive.RestaurantId ||
            row.RestaurantId == deletedAddress.RestaurantId);

        var timeResponse = await _client.GetAsync(TimeDistributionEndpoint());

        timeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var timeBody = await timeResponse.Content
            .ReadFromJsonAsync<Envelope<List<TimeDistributionCellDto>>>();
        timeBody!.Data.Should().HaveCountLessThanOrEqualTo(168);
        var vietnamDayOfWeek = (int)TargetDate.DayOfWeek;
        vietnamDayOfWeek.Should().Be(4);
        timeBody.Data.Should().ContainSingle(cell =>
            cell.DayOfWeek == vietnamDayOfWeek && cell.HourOfDay == 23)
            .Which.OrderCount.Should().Be(7);
        timeBody.Data.Should().NotContain(cell => cell.HourOfDay == 16);
    }

    [Fact]
    public async Task DemandEndpoints_WithRestaurantToken_Return403Async()
    {
        await AuthenticateAsAdminAsync();
        var restaurant = await CreateRestaurantAsync("Rbac");
        var token = await LoginAsync(restaurant.Email, restaurant.Password);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var heatmapResponse = await _client.GetAsync(HeatmapEndpoint());
        var timeResponse = await _client.GetAsync(TimeDistributionEndpoint());

        heatmapResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        timeResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static string HeatmapEndpoint() =>
        $"/api/v1/analytics/demand-heatmap?from={TargetDate:yyyy-MM-dd}" +
        $"&to={TargetDate:yyyy-MM-dd}";

    private static string TimeDistributionEndpoint() =>
        $"/api/v1/analytics/demand-heatmap/time-distribution?from={TargetDate:yyyy-MM-dd}" +
        $"&to={TargetDate:yyyy-MM-dd}";

    private async Task<CatalogFixture> CreateCatalogFixtureAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var vegetableCategoryName = $"ANA-Vegetable-{suffix}";
        var vegetableCategoryId = await CreateCategoryAsync(vegetableCategoryName);
        var fruitCategoryId = await CreateCategoryAsync($"ANA-Fruit-{suffix}");
        var unitId = await GetOrCreateUnitAsync();
        var vegetableProductId = await CreateProductAsync(
            $"ANA-Vegetable-Product-{suffix}",
            unitId,
            vegetableCategoryId);
        var fruitProductId = await CreateProductAsync(
            $"ANA-Fruit-Product-{suffix}",
            unitId,
            fruitCategoryId);
        var uncategorizedProductId = await CreateProductAsync(
            $"ANA-Uncategorized-Product-{suffix}",
            unitId,
            null);
        var marketId = await CreateMarketAsync($"ANA-Heatmap-Market-{suffix}");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var vegetable = new MarketProduct(marketId, vegetableProductId, 10m, 100, null);
        var fruit = new MarketProduct(marketId, fruitProductId, 10m, 100, null);
        var uncategorized = new MarketProduct(
            marketId,
            uncategorizedProductId,
            10m,
            100,
            null);
        db.Set<MarketProduct>().AddRange(vegetable, fruit, uncategorized);
        await db.SaveChangesAsync();

        return new CatalogFixture(
            vegetable.Id,
            fruit.Id,
            uncategorized.Id,
            vegetableCategoryName);
    }

    private async Task<Guid> CreateCategoryAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/categories", new { name });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<IdBody>>();
        return body!.Data!.Id;
    }

    private async Task<Guid> CreateProductAsync(string name, Guid unitId, Guid? categoryId)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/products", new
        {
            name,
            unitId,
            categoryId,
            description = (string?)null
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<IdBody>>();
        return body!.Data!.Id;
    }

    private async Task<Guid> CreateMarketAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/markets", new
        {
            name,
            location = "Analytics Zone",
            address = "1 Analytics St",
            latitude = (decimal?)null,
            longitude = (decimal?)null
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<IdBody>>();
        return body!.Data!.Id;
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

    private async Task<RestaurantFixture> CreateRestaurantAsync(string label)
    {
        var email = $"analytics-heatmap-{Guid.NewGuid():N}@test.freshflow";
        const string password = "RestaurantP@ss1";
        var restaurantName = $"Heatmap {label} {Guid.NewGuid():N}";
        var create = await _client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email,
            password,
            role = "restaurant",
            restaurantName
        });
        create.EnsureSuccessStatusCode();

        var response = await _client.GetAsync(
            $"/api/v1/admin/users?search={Uri.EscapeDataString(email)}&page=1&pageSize=10");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<UserListBody>>();
        var restaurantId = body!.Data!.Data.Should().ContainSingle()
            .Which.RestaurantId.Should().NotBeNull().And.Subject!.Value;
        return new RestaurantFixture(restaurantId, restaurantName, email, password);
    }

    private static Order NewOrder(Guid restaurantId, params OrderItemSeed[] items)
    {
        var order = new Order(restaurantId, null, null);
        foreach (var item in items)
        {
            order.AddItem(
                item.MarketProductId,
                item.ProductName,
                item.Quantity,
                item.UnitPrice).IsSuccess.Should().BeTrue();
        }

        order.ClearDomainEvents();
        return order;
    }

    private static Task SetRestaurantStatusAsync(
        AppDbContext db,
        Guid restaurantId,
        string status) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE restaurants SET status = {status} WHERE "Id" = {restaurantId}
            """);

    private static Task InsertAddressAsync(
        AppDbContext db,
        Guid restaurantId,
        decimal latitude,
        decimal longitude,
        DateTime? deletedAt = null) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO delivery_addresses
                ("Id", "RestaurantId", "AddressLine", "Latitude", "Longitude",
                 "IsDefault", "CreatedAt", "UpdatedAt", "DeletedAt")
            VALUES
                ({Guid.NewGuid()}, {restaurantId}, {"1 Analytics St"}, {latitude}, {longitude},
                 {true}, {CreatedAt}, {CreatedAt}, {deletedAt})
            """);

    private static Task SetOrderAsync(
        AppDbContext db,
        Guid orderId,
        DateTime createdAt,
        string status,
        DateTime? deletedAt = null) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE orders
            SET "CreatedAt" = {createdAt},
                "Status" = {status},
                "deleted_at" = {deletedAt}
            WHERE "Id" = {orderId}
            """);

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

    private sealed record IdBody(Guid Id);
    private sealed record UnitBody(Guid Id, string Name);
    private sealed record UserListBody(IReadOnlyList<UserSummaryBody> Data);
    private sealed record UserSummaryBody(Guid? RestaurantId);
    private sealed record RestaurantFixture(
        Guid RestaurantId,
        string RestaurantName,
        string Email,
        string Password);
    private sealed record CatalogFixture(
        Guid VegetableMarketProductId,
        Guid FruitMarketProductId,
        Guid UncategorizedMarketProductId,
        string VegetableCategoryName);
    private sealed record OrderItemSeed(
        Guid MarketProductId,
        string ProductName,
        int Quantity,
        decimal UnitPrice);
}

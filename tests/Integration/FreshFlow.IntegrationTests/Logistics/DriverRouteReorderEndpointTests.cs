using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.Catalog.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.Pricing.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.IntegrationTests.Logistics;

[Trait("Category", "Integration")]
public sealed class DriverRouteReorderEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private const string DriverPassword = "DriverP@ss1";
    private static readonly DateOnly ServiceDate = new(2026, 7, 30);
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Reorder_AssignedDriverUpdatesRouteAndManifest_OtherDriverGetsForbiddenAsync()
    {
        await AuthenticateAsAdminAsync();
        var assignedDriver = await CreateDriverAsync();
        var otherDriver = await CreateDriverAsync();
        var firstRestaurantId = await CreateRestaurantAsync("Reorder Restaurant One");
        var secondRestaurantId = await CreateRestaurantAsync("Reorder Restaurant Two");
        var seed = await SeedAssignedRouteAsync(
            assignedDriver.Id, firstRestaurantId, secondRestaurantId);
        var stopOrder = new[] { seed.MarketId, secondRestaurantId, firstRestaurantId };

        await AuthenticateAsync(otherDriver.Email, DriverPassword);
        var forbidden = await _client.PostAsJsonAsync(
            $"/api/v1/driver/routes/{seed.RouteId}/reorder",
            new { stopOrder });

        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var forbiddenManifest = await _client.GetAsync(
            $"/api/v1/logistics/routes/{seed.RouteId}/loading-manifest");
        forbiddenManifest.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await AuthenticateAsync(assignedDriver.Email, DriverPassword);
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/driver/routes/{seed.RouteId}/reorder",
            new { stopOrder });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Envelope<RouteDto>>();
        body!.Data!.Stops.Select(stop => stop.EntityId).Should().Equal(stopOrder);
        body.Data.Stops.Select(stop => stop.StopOrder).Should().Equal(0, 1, 2);

        var manifestResponse = await _client.GetAsync(
            $"/api/v1/logistics/routes/{seed.RouteId}/loading-manifest");

        manifestResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var manifest = await manifestResponse.Content
            .ReadFromJsonAsync<Envelope<LoadingManifestDto>>();
        manifest!.Data!.Stops.Select(stop => stop.RestaurantId)
            .Should().Equal(firstRestaurantId, secondRestaurantId);
        manifest.Data.Stops.Select(stop => stop.StopOrder).Should().Equal(2, 1);
    }

    [Fact]
    public async Task Reorder_RouteFreeSortedLine_ReturnsConflictAsync()
    {
        await AuthenticateAsAdminAsync();
        var assignedDriver = await CreateDriverAsync();
        var firstRestaurantId = await CreateRestaurantAsync("Locked Restaurant One");
        var secondRestaurantId = await CreateRestaurantAsync("Locked Restaurant Two");
        var seed = await SeedAssignedRouteAsync(
            assignedDriver.Id, firstRestaurantId, secondRestaurantId);
        await SeedRouteFreeSortingAsync(seed);

        await AuthenticateAsync(assignedDriver.Email, DriverPassword);
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/driver/routes/{seed.RouteId}/reorder",
            new { stopOrder = new[] { seed.MarketId, secondRestaurantId, firstRestaurantId } });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        error!.Error!.Code.Should().Be("ROUTE_LOCKED_FOR_SORTING");
    }

    private async Task SeedRouteFreeSortingAsync(SeededRoute seed)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hub = HubEntity.Create("Sorting Lock Hub", null, null, null, 1000m, null, seed.MarketId);
        db.Add(hub);
        await db.SaveChangesAsync();
        var sorting = FreshFlow.Hub.Domain.Entities.HubSortingProgress.Create(
            hub.Id, ServiceDate, Guid.NewGuid());
        sorting.MarkSorted(1m, Guid.NewGuid(), DateTime.UtcNow);
        db.Add(sorting);
        await db.SaveChangesAsync();
    }

    private async Task<SeededRoute> SeedAssignedRouteAsync(
        Guid driverId,
        Guid firstRestaurantId,
        Guid secondRestaurantId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var unit = new UnitOfMeasurement($"unit-{Guid.NewGuid():N}", "unit");
        var market = new Market(
            $"Reorder Market {Guid.NewGuid():N}", "HCMC", "1 Reorder St", null, null);
        db.Set<UnitOfMeasurement>().Add(unit);
        db.Set<Market>().Add(market);
        await db.SaveChangesAsync();

        var product = new Product("Reorder Product", unit.Id, null, null, null);
        db.Set<Product>().Add(product);
        await db.SaveChangesAsync();

        var marketProduct = new MarketProduct(market.Id, product.Id, 10_000m, 100, null);
        db.Set<MarketProduct>().Add(marketProduct);
        await db.SaveChangesAsync();

        var firstOrder = NewAtHubOrder(firstRestaurantId, marketProduct.Id);
        var secondOrder = NewAtHubOrder(secondRestaurantId, marketProduct.Id);
        db.Set<Order>().AddRange(firstOrder, secondOrder);
        db.Entry(firstOrder).Property(nameof(Order.Status)).CurrentValue = OrderStatus.AtHub;
        db.Entry(secondOrder).Property(nameof(Order.Status)).CurrentValue = OrderStatus.AtHub;

        IReadOnlyList<RouteStop> stops =
        [
            new(0, StopEntityType.market, market.Id, market.Name, 10.75m, 106.67m, null, null),
            new(1, StopEntityType.restaurant, firstRestaurantId, "Reorder Restaurant One",
                10.76m, 106.68m, null, null),
            new(2, StopEntityType.restaurant, secondRestaurantId, "Reorder Restaurant Two",
                10.77m, 106.69m, null, null)
        ];
        var route = DeliveryRoute.CreateDirect(ServiceDate, stops, null);
        route.ApplyOptimization(route.Stops, 10m, 20, 50_000m, OptimizationCriteria.distance);
        route.Select();
        route.MarkReviewed();
        route.Assign(Guid.NewGuid(), driverId);
        db.Set<DeliveryRoute>().Add(route);
        await db.SaveChangesAsync();

        return new SeededRoute(route.Id, market.Id);
    }

    private static Order NewAtHubOrder(Guid restaurantId, Guid marketProductId)
    {
        // Scheduled at noon Asia/Ho_Chi_Minh on the service date (05:00 UTC) so the loading
        // manifest's VN-day window filter keeps this order in scope.
        var scheduledFor = new DateTime(
            ServiceDate.Year, ServiceDate.Month, ServiceDate.Day, 5, 0, 0, DateTimeKind.Utc);
        var order = new Order(restaurantId, scheduledFor, null);
        order.AddItem(marketProductId, "Reorder Product", 1, 10_000m)
            .IsSuccess.Should().BeTrue();
        order.ClearDomainEvents();
        return order;
    }

    private async Task<DriverUser> CreateDriverAsync()
    {
        var email = $"reorder-driver-{Guid.NewGuid():N}@test.freshflow";
        var response = await _client.PostAsJsonAsync(
            "/api/v1/admin/users",
            new { email, password = DriverPassword, role = "driver" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<CreatedUserBody>>();
        return new DriverUser(body!.Data!.Id, email);
    }

    private async Task<Guid> CreateRestaurantAsync(string name)
    {
        var email = $"reorder-restaurant-{Guid.NewGuid():N}@test.freshflow";
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

    private async Task AuthenticateAsAdminAsync() =>
        await AuthenticateAsync("admin@test.freshflow", "AdminP@ss1");

    private async Task AuthenticateAsync(string identifier, string password)
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { identifier, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<TokenBody>>();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.Data!.AccessToken);
    }

    private sealed record SeededRoute(Guid RouteId, Guid MarketId);

    private sealed record DriverUser(Guid Id, string Email);

    private sealed record CreatedUserBody(
        Guid Id,
        string Email,
        string Role,
        bool IsActive,
        DateTime CreatedAt);

    private sealed record UserListBody(IReadOnlyList<UserSummaryBody> Data);

    private sealed record UserSummaryBody(Guid? RestaurantId);
}

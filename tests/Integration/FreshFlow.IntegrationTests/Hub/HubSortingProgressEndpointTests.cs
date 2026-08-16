using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.Catalog.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.Pricing.Domain.Entities;
using FreshFlow.Procurement.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

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
        var hub = await CreateHubAsync();
        var order = await CreateOrderItemAsync(hub.HubId, hub.MarketId);

        var first = await _client.PostAsJsonAsync(
            $"/api/v1/hubs/{hub.HubId}/sorting",
            new { serviceDate = ServiceDate, orderItemId = order.OrderItemId, sortedQuantityKg = 4m });
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstLine = (await first.Content.ReadFromJsonAsync<Envelope<SortingLineBody>>())!.Data!;
        firstLine.Status.Should().Be("PENDING");
        firstLine.RequiredQuantityKg.Should().Be(9m);
        firstLine.RemainingQuantityKg.Should().Be(5m);

        var second = await _client.PostAsJsonAsync(
            $"/api/v1/hubs/{hub.HubId}/sorting",
            new { serviceDate = ServiceDate, orderItemId = order.OrderItemId, sortedQuantityKg = 9m });
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        var progress = await _client.GetFromJsonAsync<Envelope<List<SortingLineBody>>>(
            $"/api/v1/hubs/{hub.HubId}/sorting-progress?serviceDate={ServiceDate:yyyy-MM-dd}");

        var line = progress!.Data!.Should().ContainSingle().Which;
        line.HubId.Should().Be(hub.HubId);
        line.ServiceDate.Should().Be(ServiceDate);
        line.RouteId.Should().BeNull();
        line.OrderId.Should().Be(order.OrderId);
        line.OrderItemId.Should().Be(order.OrderItemId);
        line.RequiredQuantityKg.Should().Be(9m);
        line.RemainingQuantityKg.Should().Be(0m);
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

    private async Task<HubBody> CreateHubAsync()
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
        return hub!.Data!;
    }


    private async Task<SeededOrder> CreateOrderItemAsync(Guid hubId, Guid marketId)
    {
        var restaurantId = await CreateRestaurantAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var unit = new UnitOfMeasurement($"sorting-unit-{Guid.NewGuid():N}", "kg");
        var product = new Product("Sorting fish", unit.Id, null, null, null);
        db.AddRange(unit, product);
        await db.SaveChangesAsync();

        var marketProduct = new MarketProduct(marketId, product.Id, 10_000m, 100, null);
        db.Add(marketProduct);
        await db.SaveChangesAsync();

        var order = new Order(
            restaurantId,
            new DateTime(2026, 7, 29, 3, 0, 0, DateTimeKind.Utc),
            null);
        order.AddItem(marketProduct.Id, "Sorting fish", 9, 10_000m)
            .IsSuccess.Should().BeTrue();
        db.Add(order);
        db.Entry(order).Property(nameof(Order.Status)).CurrentValue = OrderStatus.AtHub;
        await db.SaveChangesAsync();

        var batch = ProcurementBatch.Build(
            ServiceDate, marketId, [(marketProduct.Id, "Sorting fish", 9, order.Id)], hubId).Value;
        batch.ClearDomainEvents();
        db.Add(batch);
        await db.SaveChangesAsync();

        return new SeededOrder(order.Id, order.Items.Single().Id);
    }

    private async Task<Guid> CreateRestaurantAsync()
    {
        var email = $"sorting-{Guid.NewGuid():N}@test.freshflow";
        var create = await _client.PostAsJsonAsync(
            "/api/v1/admin/users",
            new
            {
                email,
                password = "RestaurantP@ss1",
                role = "restaurant",
                restaurantName = "Sorting Restaurant"
            });
        create.EnsureSuccessStatusCode();

        var response = await _client.GetAsync(
            $"/api/v1/admin/users?search={Uri.EscapeDataString(email)}&page=1&pageSize=10");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<UserListBody>>();
        return body!.Data!.Data.Should().ContainSingle()
            .Which.RestaurantId.Should().NotBeNull().And.Subject!.Value;
    }

    private sealed record IdBody(Guid Id);
    private sealed record HubBody(Guid HubId, Guid MarketId);
    private sealed record SeededOrder(Guid OrderId, Guid OrderItemId);
    private sealed record UserListBody(IReadOnlyList<UserSummaryBody> Data);
    private sealed record UserSummaryBody(Guid? RestaurantId);
    private sealed record SortingLineBody(
        Guid HubId,
        DateOnly ServiceDate,
        Guid? RouteId,
        Guid OrderItemId,
        decimal SortedQuantityKg,
        string Status,
        Guid? OrderId,
        decimal? RequiredQuantityKg,
        decimal? RemainingQuantityKg);
}

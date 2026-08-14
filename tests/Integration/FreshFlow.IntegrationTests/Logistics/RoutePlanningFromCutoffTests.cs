using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.Catalog.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.Pricing.Domain.Entities;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.IntegrationTests.Logistics;

[Trait("Category", "Integration")]
public sealed class RoutePlanningFromCutoffTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private static readonly DateOnly ServiceDate = new(2026, 8, 20);
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task PlanRoutes_ClosedSessionWithBatchedOrder_StampsSessionAndSupersedesChangedInputAsync()
    {
        await AuthenticateAsAdminAsync();
        var restaurantId = await CreateRestaurantAsync();
        var seed = await SeedAsync(restaurantId);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var reader = scope.ServiceProvider.GetRequiredService<IMarketSessionReader>();
            var found = await reader.FindByIdAsync(seed.SessionId, default);

            found.Should().Be(new MarketSessionLookupDto(
                seed.SessionId, seed.HubId, ServiceDate, nameof(MarketSessionStatus.Closed)));
        }

        var firstResponse = await _client.PostAsJsonAsync(
            "/api/v1/logistics/routes/plan",
            new { marketSessionId = seed.SessionId, optimizationCriteria = "DISTANCE" });

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var first = (await firstResponse.Content.ReadFromJsonAsync<Envelope<RoutePlanDto>>())!.Data!;
        first.PlanId.Should().NotBeNull();
        first.Routes.Should().ContainSingle();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var plan = await db.Set<RoutePlan>().AsNoTracking().SingleAsync(x => x.Id == first.PlanId);
            var route = await db.Set<DeliveryRoute>().AsNoTracking().SingleAsync(x => x.RoutePlanId == plan.Id);
            plan.MarketSessionId.Should().Be(seed.SessionId);
            route.MarketSessionId.Should().Be(seed.SessionId);

            var order = await db.Set<Order>().Include(x => x.Items).SingleAsync(x => x.Id == seed.OrderId);
            order.RecordActualQuantity(order.Items.Single().Id, 8m).IsSuccess.Should().BeTrue();
            await db.SaveChangesAsync();
        }

        var secondResponse = await _client.PostAsJsonAsync(
            "/api/v1/logistics/routes/plan",
            new { marketSessionId = seed.SessionId, optimizationCriteria = "DISTANCE" });

        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var second = (await secondResponse.Content.ReadFromJsonAsync<Envelope<RoutePlanDto>>())!.Data!;
        second.PlanId.Should().NotBe(first.PlanId!.Value);
        second.InputRevision.Should().NotBe(first.InputRevision);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var plans = await db.Set<RoutePlan>().AsNoTracking()
                .Where(x => x.MarketSessionId == seed.SessionId)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync();
            plans.Should().HaveCount(2);
            plans[0].Status.Should().Be(RoutePlanStatus.superseded);
            plans[1].Status.Should().Be(RoutePlanStatus.proposed);
        }
    }

    private async Task<Seed> SeedAsync(Guid restaurantId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await ActivateRestaurantAsync(db, restaurantId);

        var unit = new UnitOfMeasurement($"kg-{Guid.NewGuid():N}", "kg");
        var packingCode = new PackingCode($"BOX-{Guid.NewGuid():N}", null, 20m);
        var market = new Market($"Route Market {Guid.NewGuid():N}", "HCMC", "1 Route St", null, null);
        db.AddRange(unit, packingCode, market);
        await db.SaveChangesAsync();

        var hub = HubEntity.Create(
            "Route Hub", "1 Route St", 10.75m, 106.67m, 1000m, null, market.Id);
        var product = new Product(
            "Route fish", unit.Id, null, null, null, packingCodeId: packingCode.Id);
        db.AddRange(hub, product);
        await db.SaveChangesAsync();

        var marketProduct = new MarketProduct(market.Id, product.Id, 100_000m, 100, null);
        var session = MarketSession.Create(
            market.Id,
            hub.Id,
            ServiceDate,
            new DateTime(2026, 8, 19, 15, 0, 0, DateTimeKind.Utc),
            MarketSessionCreatedSource.Auto,
            true).Value;
        session.Close(null, "cutoff", DateTime.UtcNow);
        var vehicle = new Vehicle(
            $"RT-{Guid.NewGuid():N}"[..12], 200m, VehicleType.van, null, hub.Id);
        db.AddRange(marketProduct, session, vehicle);
        await db.SaveChangesAsync();

        var order = new Order(
            restaurantId,
            new DateTime(2026, 8, 20, 3, 0, 0, DateTimeKind.Utc),
            null);
        order.AddItem(marketProduct.Id, product.Name, 10, marketProduct.CurrentPrice)
            .IsSuccess.Should().BeTrue();
        order.ClearDomainEvents();
        db.Set<Order>().Add(order);
        db.Entry(order).Property(nameof(Order.Status)).CurrentValue = OrderStatus.Batched;
        await db.SaveChangesAsync();

        var batch = ProcurementBatch.Build(
            ServiceDate,
            market.Id,
            [(marketProduct.Id, product.Name, 10, order.Id)],
            hub.Id,
            $"RT-{Guid.NewGuid():N}",
            session.Id).Value;
        batch.ClearDomainEvents();
        db.Set<ProcurementBatch>().Add(batch);
        await db.SaveChangesAsync();

        return new Seed(session.Id, hub.Id, order.Id);
    }

    private static async Task ActivateRestaurantAsync(AppDbContext db, Guid restaurantId)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE restaurants SET status = 'active' WHERE \"Id\" = {restaurantId}");
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO delivery_addresses
                ("Id", "RestaurantId", "AddressLine", "Latitude", "Longitude",
                 "IsDefault", "CreatedAt", "UpdatedAt", "DeletedAt")
            VALUES
                ({Guid.NewGuid()}, {restaurantId}, {"2 Route St"}, {10.76m}, {106.68m},
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

    private async Task<Guid> CreateRestaurantAsync()
    {
        var email = $"route-{Guid.NewGuid():N}@test.freshflow";
        var create = await _client.PostAsJsonAsync(
            "/api/v1/admin/users",
            new
            {
                email,
                password = "RestaurantP@ss1",
                role = "restaurant",
                restaurantName = "Route Restaurant"
            });
        create.EnsureSuccessStatusCode();

        var response = await _client.GetAsync(
            $"/api/v1/admin/users?search={Uri.EscapeDataString(email)}&page=1&pageSize=10");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<UserListBody>>();
        return body!.Data!.Data.Should().ContainSingle()
            .Which.RestaurantId.Should().NotBeNull().And.Subject!.Value;
    }

    private sealed record Seed(Guid SessionId, Guid HubId, Guid OrderId);
    private sealed record UserListBody(IReadOnlyList<UserSummaryBody> Data);
    private sealed record UserSummaryBody(Guid? RestaurantId);
}

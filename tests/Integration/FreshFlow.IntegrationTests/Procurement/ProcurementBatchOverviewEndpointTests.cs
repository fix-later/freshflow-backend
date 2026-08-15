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
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.Procurement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.IntegrationTests.Procurement;

[Trait("Category", "Integration")]
public sealed class ProcurementBatchOverviewEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetOverview_MultiAgentBatch_ReturnsUnifiedShapeAsync()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginAsAdminAsync());
        var restaurantId = await CreateRestaurantAsync();
        var unit = new UnitOfMeasurement($"overview-{Guid.NewGuid():N}", "kg");
        var market = new Market(
            $"Overview Market {Guid.NewGuid():N}",
            "HCMC",
            "1 Test Street",
            null,
            null);
        var tomato = new Product("Tomato", unit.Id, null, null, null);
        var potato = new Product("Potato", unit.Id, null, null, null);
        var marketTomato = new MarketProduct(market.Id, tomato.Id, 10m, 100, null);
        var marketPotato = new MarketProduct(market.Id, potato.Id, 20m, 100, null);
        var hub = HubEntity.Create(
            $"Overview Hub {Guid.NewGuid():N}",
            null,
            null,
            null,
            1_000m,
            null,
            market.Id);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Set<UnitOfMeasurement>().Add(unit);
            db.Set<Market>().Add(market);
            db.Set<Product>().AddRange(tomato, potato);
            await db.SaveChangesAsync();
            db.Set<MarketProduct>().AddRange(marketTomato, marketPotato);
            db.Set<HubEntity>().Add(hub);
            await db.SaveChangesAsync();
        }

        var agentA = Guid.NewGuid();
        var agentB = Guid.NewGuid();
        var orderA = BuildBatchedOrder(restaurantId, marketTomato.Id, "Tomato");
        var orderB = BuildBatchedOrder(restaurantId, marketPotato.Id, "Potato");
        var batch = ProcurementBatch.Build(
            new DateOnly(2044, 8, 13),
            market.Id,
            [
                (marketTomato.Id, "Tomato", 2, orderA.Id),
                (marketPotato.Id, "Potato", 3, orderB.Id)
            ],
            hub.Id,
            $"PB-OVERVIEW-{Guid.NewGuid():N}").Value;
        var capturedAt = new DateTime(2044, 8, 12, 1, 0, 0, DateTimeKind.Utc);
        batch.Manifest(new Dictionary<Guid, decimal>
        {
            [marketTomato.Id] = 10m,
            [marketPotato.Id] = 20m
        }, capturedAt);
        batch.AssignItems(new Dictionary<Guid, Guid>
        {
            [marketTomato.Id] = agentA,
            [marketPotato.Id] = agentB
        }, capturedAt.AddMinutes(1));
        batch.ConfirmPurchase(agentA, new Dictionary<Guid, (int, decimal)>
        {
            [marketTomato.Id] = (2, 12m)
        }, capturedAt.AddMinutes(2));
        batch.ClearDomainEvents();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Set<Order>().AddRange(orderA, orderB);
            db.Set<ProcurementBatch>().Add(batch);
            await db.SaveChangesAsync();
        }

        try
        {
            var response = await _client.GetAsync(
                $"/api/v1/procurement/batches/{batch.Id}/overview");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content
                .ReadFromJsonAsync<Envelope<ProcurementBatchOverviewDto>>();
            body!.Data!.Should().BeEquivalentTo(new
            {
                BatchId = batch.Id,
                Code = batch.Code,
                MarketId = batch.MarketId,
                HubId = batch.HubId,
                BatchDate = batch.BatchDate,
                Status = "Purchasing",
                TotalItemCount = 2,
                ItemsPurchased = 1,
                ItemsPending = 1,
                ExceptionCount = 0,
                RestaurantOrderTotal = 20m,
                ActualPurchaseTotal = 24m
            });
            body.Data.Agents.Should().BeEquivalentTo([
                new
                {
                    AgentUserId = agentA,
                    ItemsAssigned = 1,
                    ItemsPurchased = 1,
                    ItemsPending = 0,
                    RestaurantOrderTotal = 10m,
                    ActualPurchaseTotal = 24m,
                    ReferenceCostTotal = (decimal?)20m,
                    ActualCostTotal = (decimal?)24m,
                    VarianceTotal = (decimal?)4m
                },
                new
                {
                    AgentUserId = agentB,
                    ItemsAssigned = 1,
                    ItemsPurchased = 0,
                    ItemsPending = 1,
                    RestaurantOrderTotal = 10m,
                    ActualPurchaseTotal = 0m,
                    ReferenceCostTotal = (decimal?)60m,
                    ActualCostTotal = (decimal?)null,
                    VarianceTotal = (decimal?)null
                }
            ]);
            body.Data.Orders.Should().BeEquivalentTo([
                new { OrderId = orderA.Id, Status = "Batched" },
                new { OrderId = orderB.Id, Status = "Batched" }
            ]);
        }
        finally
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Remove(batch);
            db.RemoveRange(orderA, orderB);
            await db.SaveChangesAsync();
        }
    }

    private static Order BuildBatchedOrder(
        Guid restaurantId,
        Guid marketProductId,
        string productName)
    {
        var order = new Order(restaurantId, DateTime.UtcNow.AddDays(1), null);
        order.AddItem(marketProductId, productName, 1, 10m);
        order.Confirm();
        order.AdvanceStatus(OrderStatus.Batched);
        order.ClearDomainEvents();
        return order;
    }

    private async Task<string> LoginAsAdminAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            identifier = "admin@test.freshflow",
            password = "AdminP@ss1"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Envelope<TokenBody>>())!
            .Data!.AccessToken;
    }

    private async Task<Guid> CreateRestaurantAsync()
    {
        var email = $"overview-{Guid.NewGuid():N}@test.freshflow";
        var create = await _client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email,
            password = "RestaurantP@ss1",
            role = "restaurant",
            restaurantName = "Overview Test Restaurant"
        });
        create.EnsureSuccessStatusCode();

        var response = await _client.GetAsync(
            $"/api/v1/admin/users?search={Uri.EscapeDataString(email)}&page=1&pageSize=10");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<UserListBody>>();
        return body!.Data!.Data.Single().RestaurantId!.Value;
    }

    private sealed record UserListBody(IReadOnlyList<UserSummaryBody> Data);

    private sealed record UserSummaryBody(Guid Id, Guid? RestaurantId);
}

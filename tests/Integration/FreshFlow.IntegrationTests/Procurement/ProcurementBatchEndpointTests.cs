using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.Catalog.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.Pricing.Domain.Entities;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.IntegrationTests.Procurement;

[Trait("Category", "Integration")]
public sealed class ProcurementBatchEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task AutoBatch_PersistsFlipsListsAndRejectsDoubleCoverAsync()
    {
        var token = await LoginAsAdminAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        var restaurantId = await CreateRestaurantAsync();
        var targetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).AddDays(1);
        var seed = await SeedConfirmedOrderAsync(restaurantId, targetDate);

        var dryRun = await _client.PostAsJsonAsync(
            "/api/v1/admin/order-groups/auto-batch",
            new { targetDate, dryRun = true, force = false });

        dryRun.StatusCode.Should().Be(HttpStatusCode.OK);
        var dryRunBody = await dryRun.Content.ReadFromJsonAsync<Envelope<BatchingResult>>();
        dryRunBody!.Data!.Preview.Should().ContainSingle();
        dryRunBody.Data.BatchesCreated.Should().Be(0);
        await AssertOrderAndBatchStateAsync(seed.OrderId, OrderStatus.Confirmed, 0);

        var run = await _client.PostAsJsonAsync(
            "/api/v1/admin/order-groups/auto-batch",
            new { targetDate, dryRun = false, force = false });

        run.StatusCode.Should().Be(HttpStatusCode.OK);
        var runBody = await run.Content.ReadFromJsonAsync<Envelope<BatchingResult>>();
        runBody!.Data!.BatchesCreated.Should().Be(1);
        runBody.Data.OrdersBatched.Should().Be(1);
        await AssertOrderAndBatchStateAsync(seed.OrderId, OrderStatus.Batched, 1);

        var list = await _client.GetAsync(
            "/api/v1/admin/order-groups?page=1&pageSize=20");

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var listBody = await list.Content
            .ReadFromJsonAsync<Envelope<ProcurementBatchListDto>>();
        listBody!.Data!.Batches.Should().ContainSingle();
        listBody.Data.Batches[0].Status.Should().Be("Built");
        listBody.Data.Batches[0].Members.Should().ContainSingle(member =>
            member.OrderId == seed.OrderId && member.Status == "Batched");
        listBody.Data.Batches[0].Items.Should().ContainSingle(item =>
            item.MarketProductId == seed.MarketProductId &&
            item.TotalQuantity == 5 &&
            item.ReferenceUnitPrice == null);
        var batchId = listBody.Data.Batches[0].Id;
        var agentA = await CreateUserAccountAsync("market_agent", seed.MarketId);
        var agentUserId = agentA.Id;
        var purchaseRequest = new
        {
            lines = new[]
            {
                new
                {
                    marketProductId = seed.MarketProductId,
                    actualQuantity = 5,
                    actualUnitPrice = 11_000m
                }
            }
        };

        await SetBatchAssignedAgentAsync(batchId, agentUserId);
        var builtAgentToken = await LoginAsync(agentA.Email, agentA.Password);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", builtAgentToken);
        var builtPurchase = await _client.PatchAsJsonAsync(
            $"/api/v1/procurement/tasks/{batchId}/purchase",
            purchaseRequest);
        builtPurchase.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var builtHandover = await _client.PatchAsJsonAsync(
            $"/api/v1/procurement/tasks/{batchId}/handover",
            new { hubId = (Guid?)null });
        builtHandover.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var builtHandoverError = await builtHandover.Content
            .ReadFromJsonAsync<ErrorEnvelope>();
        builtHandoverError!.Error!.Code.Should().Be("BATCH_NOT_PURCHASED");

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        var notManifested = await _client.PostAsJsonAsync(
            $"/api/v1/admin/order-groups/{batchId}/agent",
            new { agentUserId });
        notManifested.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var notManifestedError = await notManifested.Content
            .ReadFromJsonAsync<ErrorEnvelope>();
        notManifestedError!.Error!.Code.Should().Be("BATCH_NOT_MANIFESTED");

        var manifest = await _client.PostAsync(
            $"/api/v1/admin/order-groups/{batchId}/manifest",
            null);

        manifest.StatusCode.Should().Be(HttpStatusCode.OK);
        var manifestBody = await manifest.Content
            .ReadFromJsonAsync<Envelope<ProcurementBatchDto>>();
        manifestBody!.Data!.Status.Should().Be("Manifested");
        manifestBody.Data.ManifestedAt.Should().NotBeNull();
        manifestBody.Data.Items.Should().ContainSingle(item =>
            item.MarketProductId == seed.MarketProductId &&
            item.ReferenceUnitPrice == 10_000m);
        await AssertManifestStateAsync(batchId, 10_000m);

        var assignment = await _client.PostAsJsonAsync(
            $"/api/v1/admin/order-groups/{batchId}/agent",
            new { agentUserId });

        assignment.StatusCode.Should().Be(HttpStatusCode.OK);
        var assignmentBody = await assignment.Content
            .ReadFromJsonAsync<Envelope<ProcurementBatchDto>>();
        assignmentBody!.Data!.Status.Should().Be("Manifested");
        assignmentBody.Data.AssignedAgentUserId.Should().Be(agentUserId);
        assignmentBody.Data.AssignedAt.Should().NotBeNull();
        await AssertAgentAssignmentStateAsync(batchId, agentUserId);

        var assignedList = await _client.GetAsync(
            "/api/v1/admin/order-groups?page=1&pageSize=20");
        var assignedListBody = await assignedList.Content
            .ReadFromJsonAsync<Envelope<ProcurementBatchListDto>>();
        assignedListBody!.Data!.Batches.Should().Contain(batch =>
            batch.Id == batchId &&
            batch.AssignedAgentUserId == agentUserId &&
            batch.AssignedAt != null);

        var agentB = await CreateUserAccountAsync("market_agent", seed.MarketId);
        var wrongRoleUser = await CreateUserAccountAsync("hub_staff");
        var driver = await CreateUserAccountAsync("driver");
        var ineligibleUserId = wrongRoleUser.Id;
        var ineligible = await _client.PostAsJsonAsync(
            $"/api/v1/admin/order-groups/{batchId}/agent",
            new { agentUserId = ineligibleUserId });
        ineligible.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var ineligibleError = await ineligible.Content
            .ReadFromJsonAsync<ErrorEnvelope>();
        ineligibleError!.Error!.Code.Should().Be("AGENT_NOT_ELIGIBLE");

        var missingBatch = await _client.PostAsJsonAsync(
            $"/api/v1/admin/order-groups/{Guid.NewGuid()}/agent",
            new { agentUserId });
        missingBatch.StatusCode.Should().Be(HttpStatusCode.NotFound);

        _client.DefaultRequestHeaders.Authorization = null;
        var missingJwt = await _client.GetAsync("/api/v1/procurement/tasks");
        missingJwt.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var missingJwtPurchase = await _client.PatchAsJsonAsync(
            $"/api/v1/procurement/tasks/{batchId}/purchase",
            purchaseRequest);
        missingJwtPurchase.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var missingJwtHandover = await _client.PatchAsJsonAsync(
            $"/api/v1/procurement/tasks/{batchId}/handover",
            new { hubId = (Guid?)null });
        missingJwtHandover.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var wrongRoleToken = await LoginAsync(
            wrongRoleUser.Email,
            wrongRoleUser.Password);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", wrongRoleToken);
        var forbidden = await _client.GetAsync("/api/v1/procurement/tasks");
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var forbiddenPurchase = await _client.PatchAsJsonAsync(
            $"/api/v1/procurement/tasks/{batchId}/purchase",
            purchaseRequest);
        forbiddenPurchase.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var forbiddenHandover = await _client.PatchAsJsonAsync(
            $"/api/v1/procurement/tasks/{batchId}/handover",
            new { hubId = (Guid?)null });
        forbiddenHandover.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var agentBToken = await LoginAsync(agentB.Email, agentB.Password);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", agentBToken);
        var agentBList = await _client.GetAsync(
            "/api/v1/procurement/tasks?page=1&pageSize=20");
        agentBList.StatusCode.Should().Be(HttpStatusCode.OK);
        var agentBListBody = await agentBList.Content
            .ReadFromJsonAsync<Envelope<ProcurementBatchListDto>>();
        agentBListBody!.Data!.Batches.Should().BeEmpty();
        agentBListBody.Data.Pagination.Total.Should().Be(0);

        var crossAgentDetail = await _client.GetAsync(
            $"/api/v1/procurement/tasks/{batchId}");
        crossAgentDetail.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var crossAgentPurchase = await _client.PatchAsJsonAsync(
            $"/api/v1/procurement/tasks/{batchId}/purchase",
            purchaseRequest);
        crossAgentPurchase.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var agentAToken = await LoginAsync(agentA.Email, agentA.Password);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", agentAToken);
        var agentAList = await _client.GetAsync(
            "/api/v1/procurement/tasks?page=1&pageSize=20");
        agentAList.StatusCode.Should().Be(HttpStatusCode.OK);
        var agentAListBody = await agentAList.Content
            .ReadFromJsonAsync<Envelope<ProcurementBatchListDto>>();
        agentAListBody!.Data!.Batches.Should().ContainSingle(batch =>
            batch.Id == batchId &&
            batch.AssignedAgentUserId == agentUserId &&
            batch.Items.Any(item => item.ReferenceUnitPrice == 10_000m));

        var ownerDetail = await _client.GetAsync(
            $"/api/v1/procurement/tasks/{batchId}");
        ownerDetail.StatusCode.Should().Be(HttpStatusCode.OK);
        var ownerDetailBody = await ownerDetail.Content
            .ReadFromJsonAsync<Envelope<ProcurementBatchDto>>();
        ownerDetailBody!.Data!.Id.Should().Be(batchId);
        ownerDetailBody.Data.Items.Should().ContainSingle(item =>
            item.ReferenceUnitPrice == 10_000m);

        var unknownTask = await _client.GetAsync(
            $"/api/v1/procurement/tasks/{Guid.NewGuid()}");
        unknownTask.StatusCode.Should().Be(HttpStatusCode.NotFound);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        var refresh = await _client.PostAsync(
            $"/api/v1/admin/order-groups/{batchId}/manifest",
            null);
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);

        var missing = await _client.PostAsync(
            $"/api/v1/admin/order-groups/{Guid.NewGuid()}/manifest",
            null);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", agentAToken);
        var purchase = await _client.PatchAsJsonAsync(
            $"/api/v1/procurement/tasks/{batchId}/purchase",
            purchaseRequest);
        purchase.StatusCode.Should().Be(HttpStatusCode.OK);
        var purchaseBody = await purchase.Content
            .ReadFromJsonAsync<Envelope<ProcurementBatchDto>>();
        purchaseBody!.Data!.Status.Should().Be("Purchasing");
        purchaseBody.Data.Items.Should().ContainSingle(item =>
            item.MarketProductId == seed.MarketProductId &&
            item.ActualQuantity == 5 &&
            item.ActualUnitPrice == 11_000m &&
            item.PurchasedAt != null);
        await AssertPurchaseStateAsync(batchId, seed.MarketProductId, 5, 11_000m);

        var purchasedDetail = await _client.GetAsync(
            $"/api/v1/procurement/tasks/{batchId}");
        purchasedDetail.StatusCode.Should().Be(HttpStatusCode.OK);
        var purchasedDetailBody = await purchasedDetail.Content
            .ReadFromJsonAsync<Envelope<ProcurementBatchDto>>();
        purchasedDetailBody!.Data!.Items.Should().ContainSingle(item =>
            item.ActualQuantity == 5 && item.ActualUnitPrice == 11_000m);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", agentBToken);
        var crossAgentHandover = await _client.PatchAsJsonAsync(
            $"/api/v1/procurement/tasks/{batchId}/handover",
            new { hubId = (Guid?)null });
        crossAgentHandover.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var hubId = Guid.NewGuid();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", agentAToken);
        var handover = await _client.PatchAsJsonAsync(
            $"/api/v1/procurement/tasks/{batchId}/handover",
            new { hubId = (Guid?)hubId });
        handover.StatusCode.Should().Be(HttpStatusCode.OK);
        var handoverBody = await handover.Content
            .ReadFromJsonAsync<Envelope<ProcurementBatchDto>>();
        handoverBody!.Data!.Status.Should().Be("HandedOff");
        handoverBody.Data.HandedOffAt.Should().NotBeNull();
        handoverBody.Data.HubId.Should().Be(hubId);
        handoverBody.Data.Members.Should().ContainSingle(member =>
            member.OrderId == seed.OrderId && member.Status == "AtHub");
        await AssertHandoverStateAsync(batchId, seed.OrderId, hubId);

        var routeId = await SeedAssignedDeliveryRouteAsync(
            targetDate,
            seed.MarketId,
            restaurantId,
            driver.Id);
        var driverToken = await LoginAsync(driver.Email, driver.Password);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", driverToken);
        var pickup = await _client.PostAsJsonAsync(
            $"/api/v1/driver/routes/{routeId}/confirm-pickup",
            new { orderIds = new[] { seed.OrderId } });
        pickup.StatusCode.Should().Be(HttpStatusCode.Created);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        var inProgress = await _client.PostAsync(
            $"/api/v1/admin/order-groups/{batchId}/manifest",
            null);
        inProgress.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var inProgressError = await inProgress.Content
            .ReadFromJsonAsync<ErrorEnvelope>();
        inProgressError!.Error!.Code.Should().Be("BATCH_NOT_MANIFESTABLE");

        var coveredOrder = await SeedConfirmedOrderAsync(
            restaurantId,
            targetDate,
            seed.MarketId,
            seed.MarketProductId,
            seed.ProductName);
        await SeedActiveCoverageAsync(
            targetDate,
            seed.MarketId,
            seed.MarketProductId,
            seed.ProductName,
            coveredOrder.OrderId);

        var doubleCover = await _client.PostAsJsonAsync(
            "/api/v1/admin/order-groups/auto-batch",
            new { targetDate, dryRun = false, force = true });

        doubleCover.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await doubleCover.Content.ReadFromJsonAsync<ErrorEnvelope>();
        error!.Error!.Code.Should().Be("ORDER_ALREADY_IN_ACTIVE_GROUP");
    }

    private Task<string> LoginAsAdminAsync() =>
        LoginAsync("admin@test.freshflow", "AdminP@ss1");

    private async Task<string> LoginAsync(string identifier, string password)
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new
            {
                identifier,
                password
            });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<TokenBody>>();
        return body!.Data!.AccessToken;
    }

    private async Task<CreatedUser> CreateUserAccountAsync(
        string role,
        Guid? marketId = null)
    {
        var email = $"procurement-{role}-{Guid.NewGuid():N}@test.freshflow";
        const string password = "UserP@ss1";
        var create = await _client.PostAsJsonAsync(
            "/api/v1/admin/users",
            new
            {
                email,
                password,
                role,
                marketId
            });
        create.EnsureSuccessStatusCode();
        var body = await create.Content
            .ReadFromJsonAsync<Envelope<CreateUserBody>>();

        return new CreatedUser(body!.Data!.Id, email, password);
    }

    private async Task<Guid> CreateRestaurantAsync()
    {
        var email = $"procurement-{Guid.NewGuid():N}@test.freshflow";
        var create = await _client.PostAsJsonAsync(
            "/api/v1/admin/users",
            new
            {
                email,
                password = "RestaurantP@ss1",
                role = "restaurant",
                restaurantName = "Procurement Test Restaurant"
            });
        create.EnsureSuccessStatusCode();

        var response = await _client.GetAsync(
            $"/api/v1/admin/users?search={Uri.EscapeDataString(email)}&page=1&pageSize=10");
        response.EnsureSuccessStatusCode();
        var body = await response.Content
            .ReadFromJsonAsync<Envelope<UserListBody>>();

        return body!.Data!.Data.Should().ContainSingle()
            .Which.RestaurantId.Should().NotBeNull().And.Subject!.Value;
    }

    private async Task<SeededOrder> SeedConfirmedOrderAsync(
        Guid restaurantId,
        DateOnly targetDate,
        Guid? existingMarketId = null,
        Guid? existingMarketProductId = null,
        string productName = "Procurement Tomato")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Guid marketId;
        Guid marketProductId;

        if (existingMarketId.HasValue && existingMarketProductId.HasValue)
        {
            marketId = existingMarketId.Value;
            marketProductId = existingMarketProductId.Value;
        }
        else
        {
            var unit = new UnitOfMeasurement($"kg-{Guid.NewGuid():N}", "kg");
            db.Set<UnitOfMeasurement>().Add(unit);
            await db.SaveChangesAsync();

            var market = new Market(
                $"Procurement Market {Guid.NewGuid():N}",
                "HCMC",
                "1 Test Street",
                null,
                null);
            var product = new Product(
                productName,
                unit.Id,
                null,
                null,
                null);
            db.Set<Market>().Add(market);
            db.Set<Product>().Add(product);
            await db.SaveChangesAsync();

            var marketProduct = new MarketProduct(
                market.Id,
                product.Id,
                10_000m,
                100,
                null);
            db.Set<MarketProduct>().Add(marketProduct);
            await db.SaveChangesAsync();

            marketId = market.Id;
            marketProductId = marketProduct.Id;
        }

        var scheduledForUtc = new DateTimeOffset(
            targetDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
            TimeSpan.FromHours(7)).UtcDateTime;
        var order = new Order(restaurantId, scheduledForUtc, null);
        order.AddItem(marketProductId, productName, 5, 10_000m)
            .IsSuccess.Should().BeTrue();
        order.Confirm().IsSuccess.Should().BeTrue();
        order.ClearDomainEvents();

        db.Set<Order>().Add(order);
        await db.SaveChangesAsync();

        return new SeededOrder(
            order.Id,
            marketId,
            marketProductId,
            productName);
    }

    private async Task SeedActiveCoverageAsync(
        DateOnly targetDate,
        Guid marketId,
        Guid marketProductId,
        string productName,
        Guid orderId)
    {
        var build = ProcurementBatch.Build(
            targetDate,
            marketId,
            [(marketProductId, productName, 5, orderId)]);
        build.IsSuccess.Should().BeTrue();
        build.Value.ClearDomainEvents();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Set<ProcurementBatch>().Add(build.Value);
        await db.SaveChangesAsync();
    }

    private async Task AssertManifestStateAsync(
        Guid batchId,
        decimal expectedReferencePrice)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var batch = await db.Set<ProcurementBatch>()
            .AsNoTracking()
            .Include(candidate => candidate.Items)
            .SingleAsync(candidate => candidate.Id == batchId);
        batch.Status.Should().Be(ProcurementBatchStatus.Manifested);
        batch.ManifestedAt.Should().NotBeNull();
        batch.Items.Should().ContainSingle()
            .Which.ReferenceUnitPrice.Should().Be(expectedReferencePrice);
    }

    private async Task AssertAgentAssignmentStateAsync(
        Guid batchId,
        Guid expectedAgentUserId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var batch = await db.Set<ProcurementBatch>()
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == batchId);
        batch.Status.Should().Be(ProcurementBatchStatus.Manifested);
        batch.AssignedAgentUserId.Should().Be(expectedAgentUserId);
        batch.AssignedAt.Should().NotBeNull();
    }

    private async Task SetBatchAssignedAgentAsync(Guid batchId, Guid agentUserId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var batch = await db.Set<ProcurementBatch>()
            .SingleAsync(candidate => candidate.Id == batchId);

        db.Entry(batch).Property(candidate => candidate.AssignedAgentUserId)
            .CurrentValue = agentUserId;
        db.Entry(batch).Property(candidate => candidate.AssignedAt)
            .CurrentValue = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private async Task AssertPurchaseStateAsync(
        Guid batchId,
        Guid marketProductId,
        int actualQuantity,
        decimal actualUnitPrice)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var batch = await db.Set<ProcurementBatch>()
            .AsNoTracking()
            .Include(candidate => candidate.Items)
            .SingleAsync(candidate => candidate.Id == batchId);

        batch.Status.Should().Be(ProcurementBatchStatus.Purchasing);
        batch.Items.Should().ContainSingle(item =>
            item.MarketProductId == marketProductId &&
            item.ActualQuantity == actualQuantity &&
            item.ActualUnitPrice == actualUnitPrice &&
            item.PurchasedAt != null);
    }

    private async Task AssertHandoverStateAsync(
        Guid batchId,
        Guid orderId,
        Guid expectedHubId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var batch = await db.Set<ProcurementBatch>()
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == batchId);
        var order = await db.Set<Order>()
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == orderId);

        batch.Status.Should().Be(ProcurementBatchStatus.HandedOff);
        batch.HandedOffAt.Should().NotBeNull();
        batch.HubId.Should().Be(expectedHubId);
        order.Status.Should().Be(OrderStatus.AtHub);
    }

    private async Task<Guid> SeedAssignedDeliveryRouteAsync(
        DateOnly serviceDate,
        Guid marketId,
        Guid restaurantId,
        Guid driverUserId)
    {
        IReadOnlyList<RouteStop> stops =
        [
            new(
                0,
                StopEntityType.market,
                marketId,
                "Procurement Market",
                10.75m,
                106.67m,
                null,
                null),
            new(
                1,
                StopEntityType.restaurant,
                restaurantId,
                "Procurement Test Restaurant",
                10.76m,
                106.68m,
                null,
                null)
        ];
        var vehicle = new Vehicle(
            $"PROC-{Guid.NewGuid():N}"[..20],
            1_000m,
            VehicleType.van,
            null);
        var route = DeliveryRoute.CreateDirect(serviceDate, stops, null);
        route.Select();
        route.ApplyOptimization(stops, 10m, 20, 25_000m, OptimizationCriteria.distance);
        route.MarkReviewed();
        route.Assign(vehicle.Id, driverUserId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Set<Vehicle>().Add(vehicle);
        db.Set<DeliveryRoute>().Add(route);
        await db.SaveChangesAsync();

        return route.Id;
    }

    private async Task AssertOrderAndBatchStateAsync(
        Guid orderId,
        OrderStatus expectedStatus,
        int expectedBatchCount)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var order = await db.Set<Order>()
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == orderId);
        order.Status.Should().Be(expectedStatus);
        var batchCount = await db.Set<ProcurementBatch>().CountAsync();
        batchCount.Should().Be(expectedBatchCount);
    }

    private sealed record SeededOrder(
        Guid OrderId,
        Guid MarketId,
        Guid MarketProductId,
        string ProductName);

    private sealed record CreateUserBody(Guid Id);

    private sealed record CreatedUser(Guid Id, string Email, string Password);

    private sealed record UserListBody(IReadOnlyList<UserSummaryBody> Data);

    private sealed record UserSummaryBody(Guid Id, Guid? RestaurantId);
}

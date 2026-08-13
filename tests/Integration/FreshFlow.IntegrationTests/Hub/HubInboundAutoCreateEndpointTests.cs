using FluentAssertions;
using FreshFlow.Catalog.Domain.Entities;
using FreshFlow.Contracts;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.Hub.Infrastructure.Jobs;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Pricing.Domain.Entities;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.IntegrationTests.Hub;

/// <summary>
/// Proves the keyless seam (ToSqlQuery does not run on EF InMemory) that backs Task 1's
/// auto-create-on-handover handler and Task 2's backfill job, against real Postgres.
/// </summary>
[Trait("Category", "Integration")]
public sealed class HubInboundAutoCreateEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    [Fact]
    public async Task HandoverToHub_PurchasedBatch_CreatesPendingInboundOnceAsync()
    {
        var (hubId, marketId, marketProductId, agentUserId) = await SeedHubAndMarketAsync();
        var batchId = await SeedPurchasingBatchAsync(
            hubId, marketId, marketProductId, agentUserId, actualQuantity: 5);

        var handedOffAt = await HandoverAsync(batchId);

        var rows = await ReadInboundRowsAsync(hubId, batchId);
        rows.Should().ContainSingle();
        var row = rows[0];
        row.Status.Should().Be(HubInboundEvent.StatusPending);
        row.HubId.Should().Be(hubId);
        row.SourceMarketId.Should().Be(marketId);
        row.DeliveryScheduleId.Should().Be(batchId);
        row.ArrivedAt.Should().BeCloseTo(handedOffAt, TimeSpan.FromSeconds(1));
        row.RecordedBy.Should().Be(agentUserId);
        row.Items.Should().ContainSingle(item =>
            item.MarketProductId == marketProductId &&
            item.ProductId == null &&
            item.QuantityKg == 5m &&
            item.ProductName == "Integration Test Product");

        // Idempotency: re-publishing the same handover integration event (simulating a
        // redelivered/retried event) must not create a second row — the unique index +
        // HubConcurrencyException guard swallow the duplicate.
        using (var scope = factory.Services.CreateScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
            await publisher.Publish(new ProcurementBatchHandedOffIntegrationEvent(
                batchId, marketId, hubId, handedOffAt, [], agentUserId));
        }

        (await ReadInboundRowsAsync(hubId, batchId)).Should().ContainSingle();
    }

    [Fact]
    public async Task HandoverToHub_NoPurchasedItems_CreatesNoInboundRowAsync()
    {
        var (hubId, marketId, marketProductId, agentUserId) = await SeedHubAndMarketAsync();
        var batchId = await SeedPurchasingBatchAsync(
            hubId, marketId, marketProductId, agentUserId, actualQuantity: null);

        await HandoverAsync(batchId);

        (await ReadInboundRowsAsync(hubId, batchId)).Should().BeEmpty();
    }

    [Fact]
    public async Task Backfill_LegacyHandedOffBatchWithNoInbound_CreatesPendingRowOnceAsync()
    {
        var (hubId, marketId, marketProductId, agentUserId) = await SeedHubAndMarketAsync();
        var batchId = await SeedPurchasingBatchAsync(
            hubId, marketId, marketProductId, agentUserId, actualQuantity: 3);
        // Force straight to HandedOff via EF (bypassing the domain method) so no domain event
        // fires — simulates a batch that was handed off before Task 1's handler existed.
        var handedOffAt = DateTime.UtcNow;
        await ForceHandedOffAsync(batchId, handedOffAt);
        (await ReadInboundRowsAsync(hubId, batchId)).Should().BeEmpty();

        await RunBackfillAsync();
        await RunBackfillAsync(); // second run must not duplicate

        var rows = await ReadInboundRowsAsync(hubId, batchId);
        rows.Should().ContainSingle();
        rows[0].RecordedBy.Should().Be(agentUserId);
        rows[0].Items.Should().ContainSingle(item => item.QuantityKg == 3m);
    }

    private async Task RunBackfillAsync()
    {
        var backfill = factory.Services.GetServices<IHostedService>()
            .OfType<HubInboundBackfillHostedService>()
            .Single();
        await backfill.RunBackfillAsync(CancellationToken.None);
    }

    private async Task<(Guid HubId, Guid MarketId, Guid MarketProductId, Guid AgentUserId)>
        SeedHubAndMarketAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var unit = new UnitOfMeasurement($"kg-{Guid.NewGuid():N}", "kg");
        var market = new Market(
            $"Hub Inbound Market {Guid.NewGuid():N}", "HCMC", "1 Test Street", null, null);
        var product = new Product("Integration Test Product", unit.Id, null, null, null);
        db.Set<UnitOfMeasurement>().Add(unit);
        db.Set<Market>().Add(market);
        db.Set<Product>().Add(product);
        await db.SaveChangesAsync();

        var marketProduct = new MarketProduct(market.Id, product.Id, 10_000m, 100, null);
        var hub = HubEntity.Create(
            $"Hub Inbound Hub {Guid.NewGuid():N}", null, null, null, 1_000m, null, market.Id);
        db.Set<MarketProduct>().Add(marketProduct);
        db.Set<HubEntity>().Add(hub);
        await db.SaveChangesAsync();

        return (hub.Id, market.Id, marketProduct.Id, Guid.NewGuid());
    }

    /// <summary>
    /// Seeds a batch already at Purchasing status with AssignedAgentUserId set, bypassing the
    /// full auto-batch/manifest/purchase HTTP workflow — same direct-EF seeding idiom used by
    /// ProcurementBatchEndpointTests (SetBatchAssignedAgentAsync / SetBatchStatusForTestAsync).
    /// </summary>
    private async Task<Guid> SeedPurchasingBatchAsync(
        Guid hubId,
        Guid marketId,
        Guid marketProductId,
        Guid agentUserId,
        int? actualQuantity)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var build = ProcurementBatch.Build(
            DateOnly.FromDateTime(DateTime.UtcNow),
            marketId,
            [(marketProductId, "Integration Test Product", 5, Guid.NewGuid())],
            hubId);
        build.IsSuccess.Should().BeTrue();
        var batch = build.Value;
        batch.ClearDomainEvents();
        db.Set<ProcurementBatch>().Add(batch);
        await db.SaveChangesAsync();

        db.Entry(batch).Property(b => b.Status).CurrentValue = ProcurementBatchStatus.Purchasing;
        var item = batch.Items.Single();
        db.Entry(item).Property(i => i.AssignedAgentUserId).CurrentValue = agentUserId;
        db.Entry(item).Property(i => i.AssignedAt).CurrentValue = DateTime.UtcNow;
        if (!actualQuantity.HasValue)
        {
            batch.ReportException(
                marketProductId,
                ProcurementExceptionType.Unavailable,
                0,
                null,
                null,
                agentUserId,
                DateTime.UtcNow);
            batch.ClearDomainEvents();
        }
        await db.SaveChangesAsync();

        if (actualQuantity.HasValue)
        {
            db.Entry(item).Property(i => i.ActualQuantity).CurrentValue = actualQuantity.Value;
            db.Entry(item).Property(i => i.ActualUnitPrice).CurrentValue = 10_000m;
            db.Entry(item).Property(i => i.PurchasedAt).CurrentValue = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return batch.Id;
    }

    private async Task<DateTime> HandoverAsync(Guid batchId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var batch = await db.Set<ProcurementBatch>()
            .Include(candidate => candidate.Items)
            .SingleAsync(candidate => candidate.Id == batchId);

        var handedOffAt = DateTime.UtcNow;
        var agentUserId = batch.Items.Single().AssignedAgentUserId!.Value;
        var result = batch.HandoverToHub(agentUserId, handedOffAt);
        result.IsSuccess.Should().BeTrue();
        // Domain event dispatch happens post-commit inside this SaveChangesAsync call
        // (DomainEventDispatchInterceptor), which is what actually runs Task 1's handler.
        await db.SaveChangesAsync();

        return handedOffAt;
    }

    private async Task ForceHandedOffAsync(Guid batchId, DateTime handedOffAt)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var batch = await db.Set<ProcurementBatch>().SingleAsync(candidate => candidate.Id == batchId);
        db.Entry(batch).Property(b => b.Status).CurrentValue = ProcurementBatchStatus.HandedOff;
        db.Entry(batch).Property(b => b.HandedOffAt).CurrentValue = handedOffAt;
        await db.SaveChangesAsync();
    }

    private async Task<IReadOnlyList<HubInboundEvent>> ReadInboundRowsAsync(Guid hubId, Guid batchId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Set<HubInboundEvent>()
            .AsNoTracking()
            .Where(e => e.HubId == hubId && e.DeliveryScheduleId == batchId)
            .ToListAsync();
    }
}

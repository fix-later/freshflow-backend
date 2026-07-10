using FluentAssertions;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.Hub.Infrastructure.Repositories;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.UnitTests.Persistence;

[Trait("Category", "Unit")]
public sealed class HubRepositoryTests
{
    [Fact]
    public async Task AddAsync_AndFindByIdAsync_PersistAndReturnHubAsync()
    {
        using var db = CreateContext();
        var sut = new HubRepository(db);
        var managedBy = Guid.NewGuid();
        var hub = HubEntity.Create("Main Hub", "123 Road", 10m, 106m, 1000, managedBy);

        await sut.AddAsync(hub, default);
        await sut.SaveChangesAsync(default);

        var result = await sut.FindByIdAsync(hub.Id, default);
        result.Should().NotBeNull();
        result!.Name.Should().Be("Main Hub");
        result.ManagedBy.Should().Be(managedBy);
    }

    [Fact]
    public async Task GetPageAsync_FiltersByIsActiveAsync()
    {
        using var db = CreateContext();
        var sut = new HubRepository(db);
        var active = HubEntity.Create("Active Hub", null, null, null, 1000, null);
        var inactive = HubEntity.Create("Inactive Hub", null, null, null, 1000, null);
        inactive.Deactivate();
        await sut.AddAsync(active, default);
        await sut.AddAsync(inactive, default);
        await sut.SaveChangesAsync(default);

        var result = await sut.GetPageAsync(null, 10, true, default);

        result.Items.Should().ContainSingle(h => h.Id == active.Id);
        result.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task GetPageAsync_MoreRowsThanPageSize_ReturnsNextCursorAsync()
    {
        using var db = CreateContext();
        var sut = new HubRepository(db);
        var baseTime = new DateTime(2026, 7, 10, 10, 0, 0, DateTimeKind.Utc);
        var newest = await AddHubAsync(sut, db, "Newest Hub", baseTime.AddSeconds(2));
        await AddHubAsync(sut, db, "Oldest Hub", baseTime.AddSeconds(1));

        var result = await sut.GetPageAsync(null, 1, null, default);

        result.Items.Should().ContainSingle(h => h.Id == newest.Id);
        result.NextCursor.Should().NotBeNull();
    }

    [Fact]
    public async Task GetPageAsync_MalformedCursor_IgnoresCursorAsync()
    {
        using var db = CreateContext();
        var sut = new HubRepository(db);
        var newest = await AddHubAsync(sut, db, "Newest Hub", DateTime.UtcNow.AddSeconds(2));
        await AddHubAsync(sut, db, "Oldest Hub", DateTime.UtcNow.AddSeconds(1));

        var result = await sut.GetPageAsync("not-valid-base64", 1, null, default);

        result.Items.Should().ContainSingle(h => h.Id == newest.Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetPageAsync_InvalidPageSize_ThrowsArgumentExceptionAsync(int pageSize)
    {
        using var db = CreateContext();
        var sut = new HubRepository(db);
        Func<Task> act = () => sut.GetPageAsync(null, pageSize, null, default);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task HasPendingInboundAsync_PendingInboundExists_ReturnsTrueAsync()
    {
        using var db = CreateContext();
        var sut = new HubRepository(db);
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        var inbound = HubInboundEvent.Record(
            hub.Id,
            null,
            null,
            null,
            [new HubInboundItem(Guid.NewGuid(), null, 10m)],
            DateTime.UtcNow);
        await sut.AddAsync(hub, default);
        await db.Set<HubInboundEvent>().AddAsync(inbound);
        await sut.SaveChangesAsync(default);

        var result = await sut.HasPendingInboundAsync(hub.Id, default);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasPendingInboundAsync_ArrivedInbound_ReturnsFalseAsync()
    {
        using var db = CreateContext();
        var sut = new HubRepository(db);
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        var inbound = HubInboundEvent.Record(
            hub.Id,
            null,
            null,
            null,
            [new HubInboundItem(Guid.NewGuid(), null, 10m)],
            DateTime.UtcNow);
        inbound.ConfirmArrival();
        await sut.AddAsync(hub, default);
        await db.Set<HubInboundEvent>().AddAsync(inbound);
        await sut.SaveChangesAsync(default);

        var result = await sut.HasPendingInboundAsync(hub.Id, default);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HubInboundRepository_GetPendingPageAsync_ReturnsPendingAndArrivedAsync()
    {
        using var db = CreateContext();
        var hubs = new HubRepository(db);
        var sut = new HubInboundRepository(db);
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        var pending = HubInboundEvent.Record(
            hub.Id,
            null,
            null,
            null,
            [new HubInboundItem(Guid.NewGuid(), null, 10m)],
            DateTime.UtcNow);
        var arrived = HubInboundEvent.Record(
            hub.Id,
            null,
            null,
            null,
            [new HubInboundItem(Guid.NewGuid(), null, 20m)],
            DateTime.UtcNow);
        arrived.ConfirmArrival();
        await hubs.AddAsync(hub, default);
        await sut.AddAsync(pending, default);
        await sut.AddAsync(arrived, default);
        await sut.SaveChangesAsync(default);

        var result = await sut.GetPendingPageAsync(hub.Id, null, 10, default);

        result.Items.Should().HaveCount(2);
        result.Items.Select(e => e.Status).Should()
            .BeEquivalentTo([HubInboundEvent.StatusPending, HubInboundEvent.StatusArrivedAtHub]);
    }

    [Fact]
    public async Task HubInboundRepository_DeliveryScheduleExistsAsync_DetectsDuplicateWithinHubAsync()
    {
        using var db = CreateContext();
        var hubs = new HubRepository(db);
        var sut = new HubInboundRepository(db);
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        var deliveryScheduleId = Guid.NewGuid();
        await hubs.AddAsync(hub, default);
        await sut.AddAsync(
            HubInboundEvent.Record(
                hub.Id,
                null,
                null,
                deliveryScheduleId,
                [new HubInboundItem(Guid.NewGuid(), null, 10m)],
                DateTime.UtcNow),
            default);
        await sut.SaveChangesAsync(default);

        var result = await sut.DeliveryScheduleExistsAsync(hub.Id, deliveryScheduleId, default);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HubInboundRepository_GetHistoryPageAsync_ReturnsDateTotalsAsync()
    {
        using var db = CreateContext();
        var hubs = new HubRepository(db);
        var sut = new HubInboundRepository(db);
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        var date = new DateOnly(2026, 7, 10);
        await hubs.AddAsync(hub, default);
        await sut.AddAsync(
            HubInboundEvent.Record(
                hub.Id,
                null,
                null,
                null,
                [new HubInboundItem(Guid.NewGuid(), null, 10m)],
                date.ToDateTime(TimeOnly.MinValue)),
            default);
        await sut.AddAsync(
            HubInboundEvent.Record(
                hub.Id,
                null,
                null,
                null,
                [new HubInboundItem(Guid.NewGuid(), null, 15m)],
                date.AddDays(-1).ToDateTime(TimeOnly.MinValue)),
            default);
        await sut.SaveChangesAsync(default);

        var result = await sut.GetHistoryPageAsync(hub.Id, date, null, 10, default);

        result.Items.Should().ContainSingle();
        result.TotalQuantityKg.Should().Be(10m);
    }

    [Fact]
    public async Task HubInventoryRepository_FindByHubAndMarketProductAsync_ReturnsInventoryAsync()
    {
        using var db = CreateContext();
        var hubs = new HubRepository(db);
        var sut = new HubInventoryRepository(db);
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        var marketProductId = Guid.NewGuid();
        var inventory = HubInventory.Create(hub.Id, marketProductId);
        inventory.AddInbound(5m);
        await hubs.AddAsync(hub, default);
        await sut.AddAsync(inventory, default);
        await db.SaveChangesAsync();

        var result = await sut.FindByHubAndMarketProductAsync(hub.Id, marketProductId, default);

        result.Should().NotBeNull();
        result!.QuantityIn.Should().Be(5m);
    }

    private static AppDbContext CreateContext()
    {
        _ = typeof(FreshFlow.Hub.Infrastructure.DependencyInjection).Assembly;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"hub-repository-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<HubEntity> AddHubAsync(
        HubRepository repository,
        AppDbContext db,
        string name,
        DateTime createdAt)
    {
        var hub = HubEntity.Create(name, null, null, null, 1000, null);
        await repository.AddAsync(hub, default);
        await repository.SaveChangesAsync(default);
        db.Entry(hub).Property(h => h.CreatedAt).CurrentValue = createdAt;
        db.Entry(hub).Property(h => h.UpdatedAt).CurrentValue = createdAt;
        await db.SaveChangesAsync();

        return hub;
    }
}

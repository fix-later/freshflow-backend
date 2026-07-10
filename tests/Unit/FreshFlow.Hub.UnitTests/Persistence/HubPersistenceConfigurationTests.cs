using FluentAssertions;
using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.UnitTests.Persistence;

[Trait("Category", "Unit")]
public sealed class HubPersistenceConfigurationTests
{
    [Fact]
    public void Model_RegistersHubEntity()
    {
        using var ctx = CreateContext();

        var entity = ctx.Model.FindEntityType(typeof(HubEntity));

        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("hubs");
        ctx.Model.FindEntityType(typeof(HubInboundEvent))!
            .GetTableName()
            .Should().Be("hub_inbound_events");
        ctx.Model.FindEntityType(typeof(HubInventory))!
            .GetTableName()
            .Should().Be("hub_inventory");
    }

    [Fact]
    public void HubConfiguration_UsesSnakeCaseColumnsIndexesAndNoCrossModuleForeignKeys()
    {
        using var ctx = CreateContext();
        var entity = ctx.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(HubEntity))!;
        var table = StoreObjectIdentifier.Table("hubs", null);

        entity.FindProperty(nameof(HubEntity.Id))!
            .GetColumnName(table)
            .Should().Be("id");
        entity.FindProperty(nameof(HubEntity.Name))!
            .GetColumnName(table)
            .Should().Be("name");
        entity.FindProperty(nameof(HubEntity.Address))!
            .GetColumnName(table)
            .Should().Be("address");
        entity.FindProperty(nameof(HubEntity.Latitude))!
            .GetColumnName(table)
            .Should().Be("latitude");
        entity.FindProperty(nameof(HubEntity.Longitude))!
            .GetColumnName(table)
            .Should().Be("longitude");
        entity.FindProperty(nameof(HubEntity.CapacityKg))!
            .GetColumnName(table)
            .Should().Be("capacity_kg");
        entity.FindProperty(nameof(HubEntity.OccupiedCapacityKg))!
            .GetColumnName(table)
            .Should().Be("occupied_capacity_kg");
        entity.FindProperty(nameof(HubEntity.IsActive))!
            .GetColumnName(table)
            .Should().Be("is_active");
        entity.FindProperty(nameof(HubEntity.ManagedBy))!
            .GetColumnName(table)
            .Should().Be("managed_by");
        entity.FindProperty(nameof(HubEntity.DeletedAt))!
            .GetColumnName(table)
            .Should().Be("deleted_at");

        entity.FindProperty(nameof(HubEntity.AvailableCapacityKg)).Should().BeNull();
        entity.GetForeignKeys().Should().BeEmpty(
            "hubs.managed_by is a cross-module plain Guid");

        entity.GetCheckConstraints().Should().ContainSingle(c =>
            c.Name == "ck_hubs_capacity_kg_positive" &&
            c.Sql == "capacity_kg > 0");

        entity.GetIndexes().Should().Contain(i =>
            i.GetDatabaseName() == "idx_hubs_is_active");
        entity.GetIndexes().Should().Contain(i =>
            i.GetDatabaseName() == "idx_hubs_managed_by");
        entity.GetIndexes().Should().Contain(i =>
            i.GetDatabaseName() == "idx_hubs_deleted_at");
    }

    [Fact]
    public void HubInboundEventConfiguration_UsesSnakeCaseJsonbAndFilteredUniqueIndex()
    {
        using var ctx = CreateContext();
        var entity = ctx.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(HubInboundEvent))!;
        var table = StoreObjectIdentifier.Table("hub_inbound_events", null);

        entity.FindProperty(nameof(HubInboundEvent.Id))!.GetColumnName(table).Should().Be("id");
        entity.FindProperty(nameof(HubInboundEvent.HubId))!.GetColumnName(table).Should().Be("hub_id");
        entity.FindProperty(nameof(HubInboundEvent.SourceMarketId))!
            .GetColumnName(table)
            .Should().Be("source_market_id");
        entity.FindProperty(nameof(HubInboundEvent.DeliveryRouteId))!
            .GetColumnName(table)
            .Should().Be("delivery_route_id");
        entity.FindProperty(nameof(HubInboundEvent.DeliveryScheduleId))!
            .GetColumnName(table)
            .Should().Be("delivery_schedule_id");
        entity.FindProperty(nameof(HubInboundEvent.Items))!.GetColumnName(table).Should().Be("items");
        entity.FindProperty(nameof(HubInboundEvent.Items))!
            .FindAnnotation("Relational:ColumnType")!
            .Value
            .Should()
            .Be("jsonb");
        entity.FindProperty(nameof(HubInboundEvent.TotalQuantityKg))!
            .GetColumnName(table)
            .Should().Be("total_quantity_kg");
        entity.FindProperty(nameof(HubInboundEvent.Status))!.GetColumnName(table).Should().Be("status");
        entity.FindProperty(nameof(HubInboundEvent.ConditionStatus))!
            .GetColumnName(table)
            .Should().Be("condition_status");
        entity.FindProperty(nameof(HubInboundEvent.DeletedAt))!.GetColumnName(table).Should().Be("deleted_at");

        entity.GetForeignKeys().Should().ContainSingle(fk =>
            fk.PrincipalEntityType.ClrType == typeof(HubEntity));
        entity.GetCheckConstraints().Should().Contain(c =>
            c.Name == "ck_hub_inbound_events_status");
        entity.GetCheckConstraints().Should().Contain(c =>
            c.Name == "ck_hub_inbound_events_total_quantity_kg_positive");
        entity.GetIndexes().Should().Contain(i =>
            i.GetDatabaseName() == "ux_hub_inbound_events_hub_delivery_schedule_active" &&
            i.IsUnique &&
            i.GetFilter() == "delivery_schedule_id IS NOT NULL AND deleted_at IS NULL");
    }

    [Fact]
    public void HubInventoryConfiguration_UsesSnakeCaseComputedColumnAndUniqueIndex()
    {
        using var ctx = CreateContext();
        var entity = ctx.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(HubInventory))!;
        var table = StoreObjectIdentifier.Table("hub_inventory", null);

        entity.FindProperty(nameof(HubInventory.Id))!.GetColumnName(table).Should().Be("id");
        entity.FindProperty(nameof(HubInventory.HubId))!.GetColumnName(table).Should().Be("hub_id");
        entity.FindProperty(nameof(HubInventory.MarketProductId))!
            .GetColumnName(table)
            .Should().Be("market_product_id");
        entity.FindProperty(nameof(HubInventory.QuantityIn))!
            .GetColumnName(table)
            .Should().Be("quantity_in");
        entity.FindProperty(nameof(HubInventory.QuantityIn))!
            .FindAnnotation("Relational:ColumnType")!
            .Value.Should().Be("numeric(12,2)");
        entity.FindProperty(nameof(HubInventory.QuantityOut))!
            .GetColumnName(table)
            .Should().Be("quantity_out");
        entity.FindProperty(nameof(HubInventory.QuantityOut))!
            .FindAnnotation("Relational:ColumnType")!
            .Value.Should().Be("numeric(12,2)");
        entity.FindProperty(nameof(HubInventory.QuantityAvailable))!
            .GetColumnName(table)
            .Should().Be("quantity_available");
        entity.FindProperty(nameof(HubInventory.QuantityAvailable))!
            .FindAnnotation("Relational:ColumnType")!
            .Value.Should().Be("numeric(12,2)");
        entity.FindProperty(nameof(HubInventory.QuantityAvailable))!
            .FindAnnotation("Relational:ComputedColumnSql")!
            .Value
            .Should().Be("quantity_in - quantity_out");
        entity.FindProperty(nameof(HubInventory.DeletedAt))!.GetColumnName(table).Should().Be("deleted_at");

        entity.GetForeignKeys().Should().ContainSingle(fk =>
            fk.PrincipalEntityType.ClrType == typeof(HubEntity));
        entity.GetIndexes().Should().Contain(i =>
            i.GetDatabaseName() == "ux_hub_inventory_hub_market_product" &&
            i.IsUnique);
    }

    [Fact]
    public void AddHubModule_RegistersHubRepository()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase($"hub-di-{Guid.NewGuid()}"));

        FreshFlow.Hub.Infrastructure.DependencyInjection.AddHubModule(
            services,
            new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IHubRepository>().Should().NotBeNull();
        provider.GetRequiredService<IHubInboundRepository>().Should().NotBeNull();
        provider.GetRequiredService<IHubInventoryRepository>().Should().NotBeNull();
    }

    private static AppDbContext CreateContext()
    {
        _ = typeof(FreshFlow.Hub.Infrastructure.DependencyInjection).Assembly;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"hub-config-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }
}

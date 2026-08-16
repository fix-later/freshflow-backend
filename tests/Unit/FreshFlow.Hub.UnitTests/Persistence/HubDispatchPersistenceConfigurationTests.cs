using FluentAssertions;
using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.Hub.Infrastructure.CrossModule;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.UnitTests.Persistence;

[Trait("Category", "Unit")]
public sealed class HubDispatchPersistenceConfigurationTests
{
    [Fact]
    public void CrossDockTransferConfiguration_UsesSnakeCaseAndInternalForeignKeysOnly()
    {
        using var ctx = CreateContext();
        var entity = ctx.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(CrossDockTransfer))!;
        var table = StoreObjectIdentifier.Table("cross_dock_transfers", null);

        entity.GetTableName().Should().Be("cross_dock_transfers");
        entity.FindProperty(nameof(CrossDockTransfer.HubId))!.GetColumnName(table).Should().Be("hub_id");
        entity.FindProperty(nameof(CrossDockTransfer.InboundEventId))!
            .GetColumnName(table)
            .Should().Be("inbound_event_id");
        entity.FindProperty(nameof(CrossDockTransfer.OutboundRouteId))!
            .GetColumnName(table)
            .Should().Be("outbound_route_id");
        entity.FindProperty(nameof(CrossDockTransfer.Status))!.GetColumnName(table).Should().Be("status");

        entity.GetForeignKeys().Should().HaveCount(2);
        entity.GetForeignKeys().Should().OnlyContain(fk =>
            fk.PrincipalEntityType.ClrType == typeof(HubEntity) ||
            fk.PrincipalEntityType.ClrType == typeof(HubInboundEvent));
        entity.GetCheckConstraints().Should().Contain(c =>
            c.Name == "ck_cross_dock_transfers_status");
        entity.GetIndexes().Should().Contain(i =>
            i.GetDatabaseName() == "idx_cross_dock_transfers_outbound_route_id");
    }

    [Fact]
    public void HubOutboundEventConfiguration_UsesSnakeCaseJsonbAndNoRouteForeignKey()
    {
        using var ctx = CreateContext();
        var entity = ctx.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(HubOutboundEvent))!;
        var table = StoreObjectIdentifier.Table("hub_outbound_events", null);

        entity.GetTableName().Should().Be("hub_outbound_events");
        entity.FindProperty(nameof(HubOutboundEvent.HubId))!.GetColumnName(table).Should().Be("hub_id");
        entity.FindProperty(nameof(HubOutboundEvent.DestinationRouteId))!
            .GetColumnName(table)
            .Should().Be("destination_route_id");
        entity.FindProperty(nameof(HubOutboundEvent.Items))!.GetColumnName(table).Should().Be("items");
        entity.FindProperty(nameof(HubOutboundEvent.Items))!
            .FindAnnotation("Relational:ColumnType")!
            .Value
            .Should().Be("jsonb");
        entity.FindProperty(nameof(HubOutboundEvent.TotalQuantityKg))!
            .GetColumnName(table)
            .Should().Be("total_quantity_kg");

        entity.GetForeignKeys().Should().ContainSingle(fk =>
            fk.PrincipalEntityType.ClrType == typeof(HubEntity));
        entity.GetCheckConstraints().Should().Contain(c =>
            c.Name == "ck_hub_outbound_events_total_quantity_kg_positive");
        entity.GetIndexes().Should().Contain(i =>
            i.GetDatabaseName() == "idx_hub_outbound_events_destination_route_id");
    }

    [Fact]
    public void DeliveryRouteRowConfiguration_UsesSnakeCaseDeliveryRouteColumns()
    {
        using var ctx = CreateContext();
        var entity = ctx.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(DeliveryRouteRow))!;

        entity.GetSqlQuery().Should().Contain("id AS \"RouteId\"");
        entity.GetSqlQuery().Should().Contain("status AS \"Status\"");
        entity.GetSqlQuery().Should().Contain("driver_user_id AS \"DriverUserId\"");
        entity.GetSqlQuery().Should().Contain("delivery_routes");
        entity.GetSqlQuery().Should().Contain("deleted_at IS NULL");
        entity.GetForeignKeys().Should().BeEmpty();
    }

    [Fact]
    public void HubHandoverEventConfiguration_UsesSnakeCaseAndNoRouteForeignKey()
    {
        using var ctx = CreateContext();
        var entity = ctx.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(HubHandoverEvent))!;
        var table = StoreObjectIdentifier.Table("hub_handover_events", null);

        entity.GetTableName().Should().Be("hub_handover_events");
        entity.FindProperty(nameof(HubHandoverEvent.HubId))!.GetColumnName(table).Should().Be("hub_id");
        entity.FindProperty(nameof(HubHandoverEvent.DeliveryRouteId))!
            .GetColumnName(table)
            .Should().Be("delivery_route_id");
        entity.FindProperty(nameof(HubHandoverEvent.DriverUserId))!
            .GetColumnName(table)
            .Should().Be("driver_user_id");
        entity.FindProperty(nameof(HubHandoverEvent.OutboundEventId))!
            .GetColumnName(table)
            .Should().Be("outbound_event_id");
        entity.FindProperty(nameof(HubHandoverEvent.DriverConfirmedAt))!
            .GetColumnName(table)
            .Should().Be("driver_confirmed_at");

        entity.GetForeignKeys().Should().OnlyContain(fk =>
            fk.PrincipalEntityType.ClrType == typeof(HubEntity) ||
            fk.PrincipalEntityType.ClrType == typeof(HubOutboundEvent));
        entity.GetCheckConstraints().Should().Contain(c =>
            c.Name == "ck_hub_handover_events_status");
        entity.GetIndexes().Should().Contain(i =>
            i.GetDatabaseName() == "idx_hub_handover_events_delivery_route_id");
        entity.GetIndexes().Should().Contain(i =>
            i.GetDatabaseName() == "idx_hub_handover_events_driver_user_id");
    }

    [Fact]
    public void AddHubModule_RegistersDispatchRepositoriesAndRouteReader()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase($"hub-dispatch-di-{Guid.NewGuid()}"));

        FreshFlow.Hub.Infrastructure.DependencyInjection.AddHubModule(
            services,
            new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ICrossDockRepository>().Should().NotBeNull();
        provider.GetRequiredService<IHubOutboundRepository>().Should().NotBeNull();
        provider.GetRequiredService<IHubHandoverRepository>().Should().NotBeNull();
        provider.GetRequiredService<IDeliveryRouteReader>().Should().NotBeNull();
    }

    private static AppDbContext CreateContext()
    {
        _ = typeof(FreshFlow.Hub.Infrastructure.DependencyInjection).Assembly;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"hub-dispatch-config-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }
}

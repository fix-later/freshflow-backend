using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Infrastructure;
using FreshFlow.Logistics.Infrastructure.CrossModule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.Logistics.UnitTests.Persistence;

[Trait("Category", "Unit")]
public sealed class DeliveryPersistenceConfigurationTests
{
    [Fact]
    public void DeliveryConfiguration_UsesSnakeCaseColumnsIndexesAndOnlyRouteForeignKey()
    {
        using var ctx = CreateContext();
        var entity = ctx.Model.FindEntityType(typeof(Delivery));
        var table = StoreObjectIdentifier.Table("deliveries", null);

        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("deliveries");
        entity.FindProperty(nameof(Delivery.DeliveryRouteId))!
            .GetColumnName(table)
            .Should().Be("delivery_route_id");
        entity.FindProperty(nameof(Delivery.OrderId))!
            .GetColumnName(table)
            .Should().Be("order_id");
        entity.FindProperty(nameof(Delivery.SequenceNumber))!
            .GetColumnName(table)
            .Should().Be("sequence_number");
        entity.FindProperty(nameof(Delivery.EstimatedArrival))!
            .GetColumnName(table)
            .Should().Be("estimated_arrival");
        entity.FindProperty(nameof(Delivery.ActualArrival))!
            .GetColumnName(table)
            .Should().Be("actual_arrival");
        entity.FindProperty(nameof(Delivery.FailureReason))!
            .GetColumnName(table)
            .Should().Be("failure_reason");

        var foreignKey = entity.GetForeignKeys().Should().ContainSingle().Which;
        foreignKey.PrincipalEntityType.ClrType.Should().Be(typeof(DeliveryRoute));
        foreignKey.Properties.Should().ContainSingle(p => p.Name == nameof(Delivery.DeliveryRouteId));

        entity.GetForeignKeys().Should().NotContain(fk =>
            fk.Properties.Any(p => p.Name == nameof(Delivery.OrderId)));
        entity.GetIndexes().Should().Contain(i =>
            i.GetDatabaseName() == "idx_deliveries_delivery_route_id");

        var orderIndex = entity.GetIndexes().Single(i => i.GetDatabaseName() == "ux_deliveries_order_id");
        orderIndex.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void AddLogisticsModule_RegistersDeliveryRepository()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase($"logistics-delivery-di-{Guid.NewGuid()}"));

        services.AddLogisticsModule(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IDeliveryRepository>().Should().NotBeNull();
    }

    private static AppDbContext CreateContext()
    {
        _ = typeof(FreshFlow.Logistics.Infrastructure.DependencyInjection).Assembly;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"logistics-delivery-config-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }
}

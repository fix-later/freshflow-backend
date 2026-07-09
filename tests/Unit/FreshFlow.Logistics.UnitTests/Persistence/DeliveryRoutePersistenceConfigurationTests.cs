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
public sealed class DeliveryRoutePersistenceConfigurationTests
{
    [Fact]
    public void Model_RegistersDeliveryRouteEntity()
    {
        using var ctx = CreateContext();

        var entity = ctx.Model.FindEntityType(typeof(DeliveryRoute));

        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("delivery_routes");
    }

    [Fact]
    public void DeliveryRouteConfiguration_UsesSnakeCaseColumnsIndexesAndNoForeignKeys()
    {
        using var ctx = CreateContext();
        var entity = ctx.Model.FindEntityType(typeof(DeliveryRoute))!;
        var table = StoreObjectIdentifier.Table("delivery_routes", null);

        entity.FindProperty(nameof(DeliveryRoute.RouteType))!
            .GetColumnName(table)
            .Should().Be("route_type");
        entity.FindProperty(nameof(DeliveryRoute.Status))!
            .GetColumnName(table)
            .Should().Be("status");
        entity.FindProperty(nameof(DeliveryRoute.ServiceDate))!
            .GetColumnName(table)
            .Should().Be("service_date");
        entity.FindProperty(nameof(DeliveryRoute.Stops))!
            .GetColumnName(table)
            .Should().Be("route_metadata");
        entity.FindProperty(nameof(DeliveryRoute.VehicleId))!
            .GetColumnName(table)
            .Should().Be("vehicle_id");
        entity.FindProperty(nameof(DeliveryRoute.OrderGroupId))!
            .GetColumnName(table)
            .Should().Be("order_group_id");
        entity.FindProperty(nameof(DeliveryRoute.CreatedBy))!
            .GetColumnName(table)
            .Should().Be("created_by");

        entity.GetForeignKeys().Should().BeEmpty(
            "delivery route references cross-module resources by plain Guid only");

        entity.GetIndexes().Should().Contain(i =>
            i.GetDatabaseName() == "idx_delivery_routes_vehicle_service_date");
        entity.GetIndexes().Should().Contain(i =>
            i.GetDatabaseName() == "idx_delivery_routes_status");
        entity.GetIndexes().Should().Contain(i =>
            i.GetDatabaseName() == "idx_delivery_routes_service_date");
        entity.GetIndexes().Should().Contain(i =>
            i.GetDatabaseName() == "idx_delivery_routes_order_group_id");
        entity.GetIndexes().Should().Contain(i =>
            i.GetDatabaseName() == "idx_delivery_routes_created_by");
    }

    [Fact]
    public void CrossModuleRows_AreKeylessSqlQueries()
    {
        using var ctx = CreateContext();

        var market = ctx.Model.FindEntityType(typeof(MarketCoordinateRow));
        var restaurant = ctx.Model.FindEntityType(typeof(RestaurantCoordinateRow));

        market.Should().NotBeNull();
        market!.FindPrimaryKey().Should().BeNull();
        market.GetForeignKeys().Should().BeEmpty();
        market.GetSqlQuery().Should().Contain("FROM markets");

        restaurant.Should().NotBeNull();
        restaurant!.FindPrimaryKey().Should().BeNull();
        restaurant.GetForeignKeys().Should().BeEmpty();
        restaurant.GetSqlQuery().Should().Contain("FROM delivery_addresses");
    }

    [Fact]
    public void AddLogisticsModule_RegistersRouteRepositoryAndCoordinateReaders()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase($"logistics-route-di-{Guid.NewGuid()}"));

        services.AddLogisticsModule(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IDeliveryRouteRepository>().Should().NotBeNull();
        provider.GetRequiredService<IMarketCoordinateReader>().Should().NotBeNull();
        provider.GetRequiredService<IRestaurantCoordinateReader>().Should().NotBeNull();
    }

    private static AppDbContext CreateContext()
    {
        _ = typeof(FreshFlow.Logistics.Infrastructure.DependencyInjection).Assembly;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"logistics-route-config-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }
}

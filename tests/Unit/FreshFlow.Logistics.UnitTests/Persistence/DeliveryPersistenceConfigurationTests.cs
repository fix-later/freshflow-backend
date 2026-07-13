using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Infrastructure;
using FreshFlow.Logistics.Infrastructure.CrossModule;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
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
        entity.FindProperty(nameof(Delivery.ProofUrl))!
            .GetColumnName(table)
            .Should().Be("proof_url");
        entity.FindProperty(nameof(Delivery.ProofUrl))!
            .GetMaxLength()
            .Should().Be(512);
        entity.FindProperty(nameof(Delivery.DeletedAt))!
            .GetColumnName(table)
            .Should().Be("deleted_at");

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
    public void DeliveryIssueConfiguration_UsesSnakeCaseColumnsChecksIndexAndDeliveryForeignKey()
    {
        using var ctx = CreateContext();
        var entity = ctx.Model.FindEntityType(typeof(DeliveryIssue));
        var table = StoreObjectIdentifier.Table("delivery_issues", null);

        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("delivery_issues");
        entity.FindProperty(nameof(DeliveryIssue.DeliveryId))!
            .GetColumnName(table)
            .Should().Be("delivery_id");
        entity.FindProperty(nameof(DeliveryIssue.IssueType))!
            .GetColumnName(table)
            .Should().Be("issue_type");
        entity.FindProperty(nameof(DeliveryIssue.Description))!
            .GetColumnName(table)
            .Should().Be("description");
        entity.FindProperty(nameof(DeliveryIssue.Status))!
            .GetColumnName(table)
            .Should().Be("status");
        entity.FindProperty(nameof(DeliveryIssue.ReportedBy))!
            .GetColumnName(table)
            .Should().Be("reported_by");
        entity.FindProperty(nameof(DeliveryIssue.DeletedAt))!
            .GetColumnName(table)
            .Should().Be("deleted_at");

        entity.FindProperty(nameof(DeliveryIssue.IssueType))!
            .GetMaxLength()
            .Should().Be(30);
        entity.FindProperty(nameof(DeliveryIssue.Status))!
            .GetMaxLength()
            .Should().Be(20);

        var foreignKey = entity.GetForeignKeys().Should().ContainSingle().Which;
        foreignKey.PrincipalEntityType.ClrType.Should().Be(typeof(Delivery));
        foreignKey.GetConstraintName().Should().Be("fk_delivery_issues_delivery");

        entity.GetIndexes().Should().Contain(i =>
            i.GetDatabaseName() == "idx_delivery_issues_delivery_id");

        var designEntity = ctx.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(DeliveryIssue));
        designEntity.Should().NotBeNull();
        var checkConstraints = designEntity!.GetCheckConstraints();
        checkConstraints.Should().Contain(c => c.Name == "ck_delivery_issues_type");
        checkConstraints.Should().Contain(c => c.Name == "ck_delivery_issues_status");
    }

    [Fact]
    public void OrderStatusRow_IsKeylessSqlQueryUsingOrdersColumns()
    {
        using var ctx = CreateContext();

        var entity = ctx.Model.FindEntityType(typeof(OrderStatusRow));

        entity.Should().NotBeNull();
        entity!.FindPrimaryKey().Should().BeNull();
        entity.GetForeignKeys().Should().BeEmpty();
        entity.GetSqlQuery().Should().Contain("FROM orders");
        entity.GetSqlQuery().Should().Contain("\"Id\" AS \"OrderId\"");
        entity.GetSqlQuery().Should().Contain("\"Status\" AS \"Status\"");
        entity.GetSqlQuery().Should().Contain("\"RestaurantId\" AS \"RestaurantId\"");
        entity.GetSqlQuery().Should().Contain("\"deleted_at\" IS NULL");
    }

    [Fact]
    public void RestaurantOwnerRow_IsKeylessSqlQueryUsingRestaurantOwnerColumns()
    {
        using var ctx = CreateContext();

        var entity = ctx.Model.FindEntityType(typeof(RestaurantOwnerRow));

        entity.Should().NotBeNull();
        entity!.FindPrimaryKey().Should().BeNull();
        entity.GetForeignKeys().Should().BeEmpty();
        entity.GetSqlQuery().Should().Contain("FROM restaurants");
        entity.GetSqlQuery().Should().Contain("\"Id\" AS \"RestaurantId\"");
        entity.GetSqlQuery().Should().Contain("\"UserId\" AS \"UserId\"");
    }

    [Fact]
    public void AddLogisticsModule_RegistersDeliveryRepositoryOrderStatusReaderAndRealtimeServices()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase($"logistics-delivery-di-{Guid.NewGuid()}"));
        services.AddLogging();
        services.AddSignalR();

        services.AddLogisticsModule(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IDeliveryRepository>().Should().NotBeNull();
        provider.GetRequiredService<IDeliveryIssueRepository>().Should().NotBeNull();
        provider.GetRequiredService<IOrderStatusReader>().Should().NotBeNull();
        provider.GetRequiredService<IRestaurantOwnerReader>().Should().NotBeNull();
        provider.GetRequiredService<IDeliveryBroadcastService>().Should().NotBeNull();
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

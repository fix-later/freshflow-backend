using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.Logistics.UnitTests.Persistence;

[Trait("Category", "Unit")]
public sealed class DeliveryZonePersistenceConfigurationTests
{
    [Fact]
    public void Model_RegistersDeliveryZoneEntity()
    {
        using var ctx = CreateContext();

        var entity = ctx.Model.FindEntityType(typeof(DeliveryZone));

        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("delivery_zones");
    }

    [Fact]
    public void DeliveryZoneConfiguration_UsesSnakeCaseColumnsIndexesAndNoForeignKeys()
    {
        using var ctx = CreateContext();
        var entity = ctx.Model.FindEntityType(typeof(DeliveryZone))!;
        var table = StoreObjectIdentifier.Table("delivery_zones", null);

        entity.FindProperty(nameof(DeliveryZone.Id))!
            .GetColumnName(table)
            .Should().Be("id");
        entity.FindProperty(nameof(DeliveryZone.Code))!
            .GetColumnName(table)
            .Should().Be("code");
        entity.FindProperty(nameof(DeliveryZone.Name))!
            .GetColumnName(table)
            .Should().Be("name");
        entity.FindProperty(nameof(DeliveryZone.Description))!
            .GetColumnName(table)
            .Should().Be("description");
        entity.FindProperty(nameof(DeliveryZone.IsActive))!
            .GetColumnName(table)
            .Should().Be("is_active");
        entity.FindProperty(nameof(DeliveryZone.CreatedAt))!
            .GetColumnName(table)
            .Should().Be("created_at");
        entity.FindProperty(nameof(DeliveryZone.UpdatedAt))!
            .GetColumnName(table)
            .Should().Be("updated_at");
        entity.FindProperty(nameof(DeliveryZone.DeletedAt))!
            .GetColumnName(table)
            .Should().Be("deleted_at");

        entity.GetForeignKeys().Should().BeEmpty();

        var codeIndex = entity.GetIndexes()
            .Single(i => i.GetDatabaseName() == "ux_delivery_zones_code_active");
        codeIndex.IsUnique.Should().BeTrue();
        codeIndex.GetFilter().Should().Be("deleted_at IS NULL");

        entity.GetIndexes().Should().Contain(i =>
            i.GetDatabaseName() == "idx_delivery_zones_is_active");
    }

    [Fact]
    public void AddLogisticsModule_RegistersDeliveryZoneRepository()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase($"logistics-zone-di-{Guid.NewGuid()}"));

        services.AddLogisticsModule(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IDeliveryZoneRepository>().Should().NotBeNull();
    }

    private static AppDbContext CreateContext()
    {
        _ = typeof(FreshFlow.Logistics.Infrastructure.DependencyInjection).Assembly;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"logistics-zone-config-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }
}

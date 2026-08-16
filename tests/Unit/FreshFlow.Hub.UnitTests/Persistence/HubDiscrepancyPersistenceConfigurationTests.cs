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
public sealed class HubDiscrepancyPersistenceConfigurationTests
{
    [Fact]
    public void HubDiscrepancyConfiguration_UsesSnakeCaseConstraintsAndInternalForeignKeysOnly()
    {
        using var ctx = CreateContext();
        var entity = ctx.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(HubDiscrepancy))!;
        var table = StoreObjectIdentifier.Table("hub_discrepancies", null);

        entity.GetTableName().Should().Be("hub_discrepancies");
        entity.FindProperty(nameof(HubDiscrepancy.HubId))!.GetColumnName(table).Should().Be("hub_id");
        entity.FindProperty(nameof(HubDiscrepancy.InboundEventId))!
            .GetColumnName(table)
            .Should().Be("inbound_event_id");
        entity.FindProperty(nameof(HubDiscrepancy.OrderId))!.GetColumnName(table).Should().Be("order_id");
        entity.FindProperty(nameof(HubDiscrepancy.OrderItemId))!.GetColumnName(table).Should().Be("order_item_id");
        entity.FindProperty(nameof(HubDiscrepancy.AffectedQuantity))!
            .GetColumnName(table)
            .Should().Be("affected_quantity");
        entity.FindProperty(nameof(HubDiscrepancy.ConditionStatus))!
            .GetColumnName(table)
            .Should().Be("condition_status");
        entity.FindProperty(nameof(HubDiscrepancy.ProofImageUrl))!
            .GetColumnName(table).Should().Be("proof_image_url");
        entity.FindProperty(nameof(HubDiscrepancy.DeletedAt))!.GetColumnName(table).Should().Be("deleted_at");

        entity.GetForeignKeys().Should().HaveCount(2);
        entity.GetForeignKeys().Should().OnlyContain(fk =>
            fk.PrincipalEntityType.ClrType == typeof(HubEntity) ||
            fk.PrincipalEntityType.ClrType == typeof(HubInboundEvent));
        entity.GetCheckConstraints().Should().Contain(c =>
            c.Name == "ck_hub_discrepancies_condition_status");
        entity.GetCheckConstraints().Should().Contain(c =>
            c.Name == "ck_hub_discrepancies_status");
        entity.GetIndexes().Should().Contain(i =>
            i.GetDatabaseName() == "idx_hub_discrepancies_order_id");
        entity.GetIndexes().Should().Contain(i =>
            i.GetDatabaseName() == "idx_hub_discrepancies_order_item_id");
    }

    [Fact]
    public void OrderLookupRowConfiguration_UsesPascalCaseOrderColumns()
    {
        using var ctx = CreateContext();
        var entity = ctx.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(OrderLookupRow))!;

        entity.GetSqlQuery().Should().Contain("oi.\"Id\" AS \"OrderItemId\"");
        entity.GetSqlQuery().Should().Contain("oi.\"OrderId\" AS \"OrderId\"");
        entity.GetSqlQuery().Should().Contain("oi.\"MarketProductId\" AS \"MarketProductId\"");
        entity.GetSqlQuery().Should().Contain("oi.\"Quantity\"::numeric AS \"Quantity\"");
        entity.GetSqlQuery().Should().Contain("oi.\"ActualQuantity\" AS \"ActualQuantity\"");
        entity.GetForeignKeys().Should().BeEmpty();
    }

    [Fact]
    public void AddHubModule_RegistersDiscrepancyRepositoriesAndOrderLookupReader()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase($"hub-discrepancy-di-{Guid.NewGuid()}"));

        FreshFlow.Hub.Infrastructure.DependencyInjection.AddHubModule(
            services,
            new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IHubDiscrepancyRepository>().Should().NotBeNull();
        provider.GetRequiredService<IHubDiscrepancyReader>().Should().NotBeNull();
        provider.GetRequiredService<IOrderLookupReader>().Should().NotBeNull();
    }

    private static AppDbContext CreateContext()
    {
        _ = typeof(FreshFlow.Hub.Infrastructure.DependencyInjection).Assembly;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"hub-discrepancy-config-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }
}

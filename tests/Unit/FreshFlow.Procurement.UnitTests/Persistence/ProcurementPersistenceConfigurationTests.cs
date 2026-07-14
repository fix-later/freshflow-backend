using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Procurement.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Procurement.UnitTests.Persistence;

[Trait("Category", "Unit")]
public sealed class ProcurementPersistenceConfigurationTests
{
    [Fact]
    public void Model_MapsOnlyProcurementAggregatesToNewTables()
    {
        EfAssemblyRegistry.Register(
            typeof(FreshFlow.Procurement.Infrastructure.DependencyInjection).Assembly);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var db = new AppDbContext(options);

        db.Model.FindEntityType(typeof(ProcurementBatch))!
            .GetTableName().Should().Be("procurement_batches");
        var batch = db.Model.FindEntityType(typeof(ProcurementBatch))!;
        batch.FindProperty(nameof(ProcurementBatch.ManifestedAt))!
            .GetColumnName().Should().Be("manifested_at");
        batch.FindProperty(nameof(ProcurementBatch.AssignedAgentUserId))!
            .GetColumnName().Should().Be("assigned_agent_user_id");
        batch.FindProperty(nameof(ProcurementBatch.AssignedAt))!
            .GetColumnName().Should().Be("assigned_at");
        batch.FindProperty(nameof(ProcurementBatch.HandedOffAt))!
            .GetColumnName().Should().Be("handed_off_at");
        batch.FindProperty(nameof(ProcurementBatch.HubId))!
            .GetColumnName().Should().Be("hub_id");
        batch.GetIndexes().Should().ContainSingle(index =>
            index.GetDatabaseName() == "ix_procurement_batches_assigned_agent" &&
            index.GetFilter() == "\"assigned_agent_user_id\" IS NOT NULL");
        var item = db.Model.FindEntityType(typeof(ProcurementBatchItem))!;
        item.GetTableName().Should().Be("procurement_batch_items");
        item.FindProperty(nameof(ProcurementBatchItem.ReferenceUnitPrice))!
            .GetColumnName().Should().Be("reference_unit_price");
        item.FindProperty(nameof(ProcurementBatchItem.ActualQuantity))!
            .GetColumnName().Should().Be("actual_quantity");
        item.FindProperty(nameof(ProcurementBatchItem.ActualUnitPrice))!
            .GetColumnName().Should().Be("actual_unit_price");
        item.FindProperty(nameof(ProcurementBatchItem.ActualUnitPrice))!
            .FindAnnotation("Relational:ColumnType")!.Value
            .Should().Be("numeric(12,2)");
        item.FindProperty(nameof(ProcurementBatchItem.PurchasedAt))!
            .GetColumnName().Should().Be("purchased_at");
        var order = db.Model.FindEntityType(typeof(ProcurementBatchOrder))!;
        order.GetTableName().Should().Be("procurement_batch_orders");
        order.GetIndexes().Should().ContainSingle(index =>
            index.IsUnique &&
            index.GetDatabaseName() == "ux_procurement_batch_orders_order_active" &&
            index.GetFilter() == "\"deleted_at\" IS NULL");
    }
}

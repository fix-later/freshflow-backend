using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Hub.Infrastructure.CrossModule;

internal sealed class HubProcurementBatchRow
{
    public Guid BatchId { get; set; }
    public DateOnly BatchDate { get; set; }
    public Guid MarketId { get; set; }
    public Guid? HubId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? HandedOffAt { get; set; }
    public Guid? AssignedAgentUserId { get; set; }
}

internal sealed class HubProcurementItemRow
{
    public Guid ProcurementBatchId { get; set; }
    public Guid MarketProductId { get; set; }
    public Guid? ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int TargetQuantity { get; set; }
    public int? ActualQuantity { get; set; }
    public decimal? ActualUnitPrice { get; set; }
    public DateTime? PurchasedAt { get; set; }
    public string? PackingCode { get; set; }
    public decimal? PackingCapacityKg { get; set; }
}

internal sealed class HubProcurementOrderRow
{
    public Guid ProcurementBatchId { get; set; }
    public Guid OrderId { get; set; }
}

internal sealed class HubProcurementBatchRowConfiguration
    : IEntityTypeConfiguration<HubProcurementBatchRow>
{
    public void Configure(EntityTypeBuilder<HubProcurementBatchRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT id AS "BatchId",
                   batch_date AS "BatchDate",
                   market_id AS "MarketId",
                   hub_id AS "HubId",
                   status AS "Status",
                   handed_off_at AS "HandedOffAt",
                   assigned_agent_user_id AS "AssignedAgentUserId"
            FROM procurement_batches
            WHERE deleted_at IS NULL
            """);
    }
}

internal sealed class HubProcurementItemRowConfiguration
    : IEntityTypeConfiguration<HubProcurementItemRow>
{
    public void Configure(EntityTypeBuilder<HubProcurementItemRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT pbi.procurement_batch_id AS "ProcurementBatchId",
                   pbi.market_product_id AS "MarketProductId",
                   mp."ProductId" AS "ProductId",
                   pbi.product_name_snapshot AS "ProductName",
                   pbi.total_quantity AS "TargetQuantity",
                   pbi.actual_quantity AS "ActualQuantity",
                   pbi.actual_unit_price AS "ActualUnitPrice",
                   pbi.purchased_at AS "PurchasedAt",
                   pc."Code" AS "PackingCode",
                   pc."CapacityKg" AS "PackingCapacityKg"
            FROM procurement_batch_items pbi
            LEFT JOIN market_products mp
              ON mp."Id" = pbi.market_product_id AND mp.deleted_at IS NULL
            LEFT JOIN products p
              ON p."Id" = mp."ProductId" AND p."DeletedAt" IS NULL
            LEFT JOIN packing_codes pc
              ON pc."Id" = p."PackingCodeId" AND pc."DeletedAt" IS NULL
            WHERE pbi.deleted_at IS NULL
            """);
    }
}

internal sealed class HubProcurementOrderRowConfiguration
    : IEntityTypeConfiguration<HubProcurementOrderRow>
{
    public void Configure(EntityTypeBuilder<HubProcurementOrderRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT procurement_batch_id AS "ProcurementBatchId",
                   order_id AS "OrderId"
            FROM procurement_batch_orders
            WHERE deleted_at IS NULL
            """);
    }
}

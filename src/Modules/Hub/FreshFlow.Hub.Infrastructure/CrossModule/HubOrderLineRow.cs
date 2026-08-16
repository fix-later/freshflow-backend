using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Hub.Infrastructure.CrossModule;

internal sealed class HubOrderLineRow
{
    public Guid OrderId { get; init; }
    public Guid OrderItemId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public Guid MarketProductId { get; init; }
    public Guid ProductId { get; init; }
    public string? Unit { get; init; }
    public int Quantity { get; init; }
    public decimal? ActualQuantity { get; init; }
    public string? PackingCode { get; init; }
    public decimal? CapacityKg { get; init; }
}

internal sealed class HubOrderLineRowConfiguration
    : IEntityTypeConfiguration<HubOrderLineRow>
{
    public void Configure(EntityTypeBuilder<HubOrderLineRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT
                oi."OrderId"             AS "OrderId",
                oi."Id"                  AS "OrderItemId",
                oi."ProductNameSnapshot" AS "ProductName",
                mp."Id"                  AS "MarketProductId",
                mp."ProductId"           AS "ProductId",
                COALESCE(u."Name", p.unit) AS "Unit",
                oi."Quantity"            AS "Quantity",
                oi."ActualQuantity"      AS "ActualQuantity",
                COALESCE(oi.packing_code_snapshot, pc."Code") AS "PackingCode",
                COALESCE(oi.packing_weight_kg_snapshot, pc."CapacityKg") AS "CapacityKg"
            FROM order_items oi
            INNER JOIN orders o          ON o."Id" = oi."OrderId" AND o."deleted_at" IS NULL
            INNER JOIN market_products mp ON mp."Id" = oi."MarketProductId"
            INNER JOIN products p        ON p."Id" = mp."ProductId"
            LEFT JOIN units_of_measurement u ON u."Id" = p."UnitId"
            LEFT JOIN packing_codes pc   ON pc."Id" = p."PackingCodeId" AND pc."DeletedAt" IS NULL
            WHERE mp."deleted_at" IS NULL AND p."DeletedAt" IS NULL
            """);
        builder.Property(x => x.OrderId);
        builder.Property(x => x.OrderItemId);
        builder.Property(x => x.ProductName);
        builder.Property(x => x.MarketProductId);
        builder.Property(x => x.ProductId);
        builder.Property(x => x.ActualQuantity);
        builder.Property(x => x.PackingCode);
        builder.Property(x => x.Unit);
        builder.Property(x => x.Quantity);
        builder.Property(x => x.CapacityKg);
    }
}

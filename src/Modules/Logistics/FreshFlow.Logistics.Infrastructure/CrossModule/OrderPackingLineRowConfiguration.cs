using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class OrderPackingLineRowConfiguration
    : IEntityTypeConfiguration<OrderPackingLineRow>
{
    public void Configure(EntityTypeBuilder<OrderPackingLineRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT
                oi."OrderId"             AS "OrderId",
                oi."Id"                  AS "OrderItemId",
                oi."ProductNameSnapshot" AS "ProductName",
                COALESCE(oi."ActualQuantity", oi."Quantity") AS "Quantity",
                pc."CapacityKg"          AS "CapacityKg"
            FROM order_items oi
            INNER JOIN orders o          ON o."Id" = oi."OrderId" AND o."deleted_at" IS NULL
            INNER JOIN market_products mp ON mp."Id" = oi."MarketProductId"
            INNER JOIN products p        ON p."Id" = mp."ProductId"
            LEFT JOIN packing_codes pc   ON pc."Id" = p."PackingCodeId" AND pc."DeletedAt" IS NULL
            WHERE mp."deleted_at" IS NULL AND p."DeletedAt" IS NULL
            """);
        builder.Property(x => x.OrderId);
        builder.Property(x => x.OrderItemId);
        builder.Property(x => x.ProductName);
        builder.Property(x => x.Quantity);
        builder.Property(x => x.CapacityKg);
    }
}

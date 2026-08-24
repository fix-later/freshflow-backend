using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Invoicing.Infrastructure.CrossModule;

internal sealed class OrderInvoiceRowConfiguration : IEntityTypeConfiguration<OrderInvoiceRow>
{
    public void Configure(EntityTypeBuilder<OrderInvoiceRow> builder)
    {
        // Read-only projection over Orders' data. Quantity is the DELIVERED quantity
        // (AUDIT-2026-08-23 C4: falls back to the ordered quantity for lines procurement never
        // touched, e.g. line-level actuals were never recorded). Unit price, VAT rate and
        // delivery fee remain the immutable confirmation snapshot — LockedUnitPrice is what the
        // buyer agreed to pay, never ActualUnitPrice, which is FreshFlow's internal market cost.
        // Column casing is mixed by table and verified against each *Configuration.cs — do not guess.
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT
                o."Id" AS "OrderId",
                o."RestaurantId",
                o.delivery_fee AS "DeliveryFee",
                oi."ProductNameSnapshot" AS "ProductName",
                COALESCE(u."Name", p.unit) AS "Unit",
                COALESCE(oi."ActualQuantity", oi."Quantity") AS "Quantity",
                COALESCE(oi."LockedUnitPrice", oi."UnitPrice") AS "UnitPrice",
                oi.vat_rate_code AS "VatRateCode"
            FROM orders o
            INNER JOIN order_items oi ON oi."OrderId" = o."Id"
            INNER JOIN market_products mp ON mp."Id" = oi."MarketProductId"
            INNER JOIN products p ON p."Id" = mp."ProductId"
            LEFT JOIN units_of_measurement u ON u."Id" = p."UnitId"
            WHERE o.deleted_at IS NULL
              AND o."Status" = 'Delivered'
              AND COALESCE(oi."ActualQuantity", oi."Quantity") > 0
            """);

        builder.Property(r => r.OrderId);
        builder.Property(r => r.RestaurantId);
        builder.Property(r => r.DeliveryFee);
        builder.Property(r => r.ProductName);
        builder.Property(r => r.Unit);
        builder.Property(r => r.Quantity);
        builder.Property(r => r.UnitPrice);
        builder.Property(r => r.VatRateCode);
    }
}

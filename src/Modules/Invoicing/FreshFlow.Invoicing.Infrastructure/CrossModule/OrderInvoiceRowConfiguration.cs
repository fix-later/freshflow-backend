using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Invoicing.Infrastructure.CrossModule;

internal sealed class OrderInvoiceRowConfiguration : IEntityTypeConfiguration<OrderInvoiceRow>
{
    public void Configure(EntityTypeBuilder<OrderInvoiceRow> builder)
    {
        // Read-only projection over Orders' immutable confirmation snapshots.
        // Quantity is the delivered amount (ActualQuantity when a shortage was recorded, else ordered).
        // Unit price and VAT rate are locked at confirm (null → KCT for legacy rows).
        // Column casing is mixed by table and verified against each *Configuration.cs — do not guess.
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT
                o."Id" AS "OrderId",
                o."RestaurantId",
                oi."ProductNameSnapshot" AS "ProductName",
                COALESCE(oi."ActualQuantity", oi."Quantity") AS "Quantity",
                COALESCE(oi."LockedUnitPrice", oi."UnitPrice") AS "UnitPrice",
                oi.vat_rate_code AS "VatRateCode"
            FROM orders o
            INNER JOIN order_items oi ON oi."OrderId" = o."Id"
            WHERE o.deleted_at IS NULL
            """);

        builder.Property(r => r.OrderId);
        builder.Property(r => r.RestaurantId);
        builder.Property(r => r.ProductName);
        builder.Property(r => r.Quantity);
        builder.Property(r => r.UnitPrice);
        builder.Property(r => r.VatRateCode);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Invoicing.Infrastructure.CrossModule;

internal sealed class OrderInvoiceRowConfiguration : IEntityTypeConfiguration<OrderInvoiceRow>
{
    public void Configure(EntityTypeBuilder<OrderInvoiceRow> builder)
    {
        // Read-only projection over orders (Orders) + order_items (Orders) + market_products (Pricing)
        // + products (Catalog). ToSqlQuery avoids model conflicts with those modules' ToTable mappings.
        // Quantity is the delivered amount (ActualQuantity when a shortage was recorded, else ordered).
        // Unit price is the price locked at confirm. VAT rate is read live from the product (null → KCT).
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
                p."VatRate" AS "VatRateCode"
            FROM orders o
            INNER JOIN order_items oi ON oi."OrderId" = o."Id"
            LEFT JOIN market_products mp ON mp."Id" = oi."MarketProductId" AND mp."deleted_at" IS NULL
            LEFT JOIN products p ON p."Id" = mp."ProductId" AND p."DeletedAt" IS NULL
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

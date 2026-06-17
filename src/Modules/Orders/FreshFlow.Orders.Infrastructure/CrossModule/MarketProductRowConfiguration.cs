using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Orders.Infrastructure.CrossModule;

internal sealed class MarketProductRowConfiguration : IEntityTypeConfiguration<MarketProductRow>
{
    public void Configure(EntityTypeBuilder<MarketProductRow> builder)
    {
        // Read-only projection joining market_products (Pricing) with products (Catalog).
        // ToSqlQuery avoids a model conflict with Pricing's ToTable("market_products").
        // Deleted market product rows and deleted products are excluded.
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT
                mp."Id",
                p."Name" AS "ProductName",
                mp."CurrentPrice",
                mp."CurrentQuantity",
                mp."ReservedQuantity"
            FROM market_products mp
            INNER JOIN products p ON mp."ProductId" = p."Id"
            WHERE mp."deleted_at" IS NULL AND p."DeletedAt" IS NULL
            """);

        builder.Property(m => m.Id);
        builder.Property(m => m.ProductName);
        builder.Property(m => m.CurrentPrice);
        builder.Property(m => m.CurrentQuantity);
        builder.Property(m => m.ReservedQuantity);
    }
}

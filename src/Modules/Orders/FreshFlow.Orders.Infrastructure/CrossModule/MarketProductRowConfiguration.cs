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
                mp."MarketId",
                p."Name" AS "ProductName",
                mp."CurrentPrice",
                mp."CurrentQuantity",
                mp."ReservedQuantity",
                p."MinimumOrderQuantity",
                p."VatRate",
                pc."Code" AS "PackingCode",
                pc."CapacityKg" AS "PackingWeightKg",
                CASE WHEN h.latitude IS NOT NULL AND h.longitude IS NOT NULL
                    THEN h.latitude ELSE m."Latitude" END AS "OriginLatitude",
                CASE WHEN h.latitude IS NOT NULL AND h.longitude IS NOT NULL
                    THEN h.longitude ELSE m."Longitude" END AS "OriginLongitude"
            FROM market_products mp
            INNER JOIN products p ON mp."ProductId" = p."Id"
            INNER JOIN markets m ON mp."MarketId" = m."Id" AND m."DeletedAt" IS NULL
            LEFT JOIN packing_codes pc ON p."PackingCodeId" = pc."Id" AND pc."DeletedAt" IS NULL
            LEFT JOIN hubs h ON h.market_id = mp."MarketId"
                AND h.is_active = TRUE AND h.deleted_at IS NULL
            WHERE mp."deleted_at" IS NULL AND p."DeletedAt" IS NULL
            """);

        builder.Property(m => m.Id);
        builder.Property(m => m.MarketId);
        builder.Property(m => m.ProductName);
        builder.Property(m => m.CurrentPrice);
        builder.Property(m => m.CurrentQuantity);
        builder.Property(m => m.ReservedQuantity);
        builder.Property(m => m.MinimumOrderQuantity);
        builder.Property(m => m.VatRate);
        builder.Property(m => m.OriginLatitude);
        builder.Property(m => m.OriginLongitude);
        builder.Property(m => m.PackingCode);
        builder.Property(m => m.PackingWeightKg);
    }
}

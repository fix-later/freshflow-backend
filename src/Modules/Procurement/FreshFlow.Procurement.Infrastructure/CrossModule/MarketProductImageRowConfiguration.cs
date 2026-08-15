using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Procurement.Infrastructure.CrossModule;

internal sealed class MarketProductImageRowConfiguration
    : IEntityTypeConfiguration<MarketProductImageRow>
{
    public void Configure(EntityTypeBuilder<MarketProductImageRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT mp."Id"         AS "MarketProductId",
                   p."ImageUrl"    AS "ImageUrl",
                   pc."Code"       AS "PackingCode",
                   pc."CapacityKg" AS "PackingCapacityKg"
            FROM market_products mp
            JOIN products p ON p."Id" = mp."ProductId"
            LEFT JOIN packing_codes pc
              ON pc."Id" = p."PackingCodeId" AND pc."DeletedAt" IS NULL
            WHERE mp."deleted_at" IS NULL
              AND p."DeletedAt" IS NULL
            """);
    }
}

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
            SELECT mp."Id"       AS "MarketProductId",
                   p."ImageUrl"  AS "ImageUrl"
            FROM market_products mp
            JOIN products p ON p."Id" = mp."ProductId"
            WHERE mp."deleted_at" IS NULL
              AND p."DeletedAt" IS NULL
            """);
    }
}

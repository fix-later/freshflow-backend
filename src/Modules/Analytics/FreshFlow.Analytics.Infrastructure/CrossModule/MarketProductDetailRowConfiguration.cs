using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class MarketProductDetailRowConfiguration
    : IEntityTypeConfiguration<MarketProductDetailRow>
{
    public void Configure(EntityTypeBuilder<MarketProductDetailRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT
                mp."Id" AS "MarketProductId",
                p."Name" AS "ProductName",
                m."Name" AS "MarketName"
            FROM market_products mp
            JOIN products p ON p."Id" = mp."ProductId"
            JOIN markets m ON m."Id" = mp."MarketId"
            WHERE mp."deleted_at" IS NULL
              AND p."DeletedAt" IS NULL
              AND m."DeletedAt" IS NULL
            """);
    }
}

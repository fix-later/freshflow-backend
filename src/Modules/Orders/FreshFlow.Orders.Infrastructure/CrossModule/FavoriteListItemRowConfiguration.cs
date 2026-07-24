using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Orders.Infrastructure.CrossModule;

internal sealed class FavoriteListItemRowConfiguration : IEntityTypeConfiguration<FavoriteListItemRow>
{
    public void Configure(EntityTypeBuilder<FavoriteListItemRow> builder)
    {
        // Static, parameterless ToSqlQuery per SQL policy — restaurant_id is filtered outside
        // in LINQ (FavoriteReader), not baked into this SQL.
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT
                rf.restaurant_id     AS "RestaurantId",
                rf.market_product_id AS "MarketProductId",
                rf.created_at        AS "CreatedAt",
                mp."ProductId"       AS "ProductId",
                p."Name"             AS "ProductName",
                p."ImageUrl"         AS "ImageUrl",
                mp."MarketId"        AS "MarketId",
                m."Name"             AS "MarketName",
                c."Name"             AS "Category",
                u."Name"             AS "Unit",
                mp."CurrentPrice"    AS "CurrentPrice",
                (mp."CurrentQuantity" - mp."ReservedQuantity") AS "AvailableQuantity"
            FROM restaurant_favorites rf
            INNER JOIN market_products mp ON rf.market_product_id = mp."Id"
            INNER JOIN products p         ON mp."ProductId" = p."Id"
            INNER JOIN markets m          ON mp."MarketId" = m."Id"
            LEFT JOIN units_of_measurement u ON p."UnitId" = u."Id"
            LEFT JOIN product_categories  c  ON p."CategoryId" = c."Id"
            WHERE mp."deleted_at" IS NULL AND p."DeletedAt" IS NULL
            """);

        builder.Property(r => r.RestaurantId);
        builder.Property(r => r.MarketProductId);
        builder.Property(r => r.CreatedAt);
        builder.Property(r => r.ProductId);
        builder.Property(r => r.ProductName);
        builder.Property(r => r.ImageUrl);
        builder.Property(r => r.MarketId);
        builder.Property(r => r.MarketName);
        builder.Property(r => r.Category);
        builder.Property(r => r.Unit);
        builder.Property(r => r.CurrentPrice);
        builder.Property(r => r.AvailableQuantity);
    }
}

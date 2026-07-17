using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class OrderItemCategoryRowConfiguration
    : IEntityTypeConfiguration<OrderItemCategoryRow>
{
    public void Configure(EntityTypeBuilder<OrderItemCategoryRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT
                oi."OrderId" AS "OrderId",
                oi."Quantity" AS "Quantity",
                c."Name" AS "CategoryName"
            FROM order_items oi
            INNER JOIN market_products mp ON oi."MarketProductId" = mp."Id"
            INNER JOIN products p ON mp."ProductId" = p."Id"
            LEFT JOIN product_categories c ON p."CategoryId" = c."Id"
            WHERE mp."deleted_at" IS NULL AND p."DeletedAt" IS NULL
            """);
        builder.Property(row => row.OrderId);
        builder.Property(row => row.Quantity);
        builder.Property(row => row.CategoryName);
    }
}

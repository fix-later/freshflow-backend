using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class OrderMarketRowConfiguration : IEntityTypeConfiguration<OrderMarketRow>
{
    public void Configure(EntityTypeBuilder<OrderMarketRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT o."Id" AS "OrderId", o."Status" AS "Status",
                   o."ScheduledFor" AS "ScheduledFor",
                   mp."MarketId" AS "MarketId", m."Name" AS "MarketName"
            FROM order_items oi
            JOIN orders o ON o."Id" = oi."OrderId" AND o."deleted_at" IS NULL
            JOIN market_products mp ON mp."Id" = oi."MarketProductId" AND mp."deleted_at" IS NULL
            JOIN markets m ON m."Id" = mp."MarketId" AND m."DeletedAt" IS NULL
            """);
        builder.Property(row => row.OrderId);
        builder.Property(row => row.Status);
        builder.Property(row => row.ScheduledFor);
        builder.Property(row => row.MarketId);
        builder.Property(row => row.MarketName);
    }
}

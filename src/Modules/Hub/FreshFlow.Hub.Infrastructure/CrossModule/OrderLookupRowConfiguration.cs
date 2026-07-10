using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Hub.Infrastructure.CrossModule;

internal sealed class OrderLookupRowConfiguration : IEntityTypeConfiguration<OrderLookupRow>
{
    public void Configure(EntityTypeBuilder<OrderLookupRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT
                oi."Id" AS "OrderItemId",
                oi."OrderId" AS "OrderId",
                oi."MarketProductId" AS "MarketProductId",
                oi."Quantity"::numeric AS "Quantity",
                oi."ActualQuantity" AS "ActualQuantity"
            FROM order_items oi
            """);
        builder.Property(o => o.OrderItemId);
        builder.Property(o => o.OrderId);
        builder.Property(o => o.MarketProductId);
        builder.Property(o => o.Quantity);
        builder.Property(o => o.ActualQuantity);
    }
}

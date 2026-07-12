using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class OrderStatusRowConfiguration : IEntityTypeConfiguration<OrderStatusRow>
{
    public void Configure(EntityTypeBuilder<OrderStatusRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT "Id" AS "OrderId", "Status" AS "Status", "RestaurantId" AS "RestaurantId"
            FROM orders
            WHERE "deleted_at" IS NULL
            """);
        builder.Property(o => o.OrderId);
        builder.Property(o => o.Status);
        builder.Property(o => o.RestaurantId);
    }
}

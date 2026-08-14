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
            SELECT o."Id" AS "OrderId", o."Status" AS "Status",
                   o."RestaurantId" AS "RestaurantId", o."ScheduledFor" AS "ScheduledFor",
                   o."delivery_latitude" AS "DeliveryLatitude",
                   o."delivery_longitude" AS "DeliveryLongitude",
                   pb.hub_id AS "HubId"
            FROM orders o
            LEFT JOIN procurement_batch_orders pbo
              ON pbo.order_id = o."Id" AND pbo.deleted_at IS NULL
            LEFT JOIN procurement_batches pb
              ON pb.id = pbo.procurement_batch_id AND pb.deleted_at IS NULL
            WHERE o."deleted_at" IS NULL
            """);
        builder.Property(o => o.OrderId);
        builder.Property(o => o.Status);
        builder.Property(o => o.RestaurantId);
        builder.Property(o => o.HubId);
        builder.Property(o => o.ScheduledFor);
        builder.Property(o => o.DeliveryLatitude);
        builder.Property(o => o.DeliveryLongitude);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Hub.Infrastructure.CrossModule;

internal sealed class HubRestaurantOrderRow
{
    public Guid HubId { get; init; }
    public Guid OrderId { get; init; }
    public Guid RestaurantId { get; init; }
    public string RestaurantName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime? ScheduledFor { get; init; }
}

internal sealed class HubRestaurantOrderRowConfiguration
    : IEntityTypeConfiguration<HubRestaurantOrderRow>
{
    public void Configure(EntityTypeBuilder<HubRestaurantOrderRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT pb.hub_id           AS "HubId",
                   pbo.order_id        AS "OrderId",
                   o."RestaurantId"    AS "RestaurantId",
                   r."Name"            AS "RestaurantName",
                   o."Status"          AS "Status",
                   o."ScheduledFor"    AS "ScheduledFor"
            FROM procurement_batches pb
            JOIN procurement_batch_orders pbo ON pbo.procurement_batch_id = pb.id AND pbo.deleted_at IS NULL
            JOIN orders o        ON o."Id" = pbo.order_id AND o."deleted_at" IS NULL
            JOIN restaurants r   ON r."Id" = o."RestaurantId"
            WHERE pb.deleted_at IS NULL
            """);
    }
}

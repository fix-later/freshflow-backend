using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class HubInboundEventRowConfiguration : IEntityTypeConfiguration<HubInboundEventRow>
{
    public void Configure(EntityTypeBuilder<HubInboundEventRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT
                id AS "InboundEventId",
                status AS "Status",
                total_quantity_kg AS "TotalQuantityKg",
                arrived_at AS "ArrivedAt"
            FROM hub_inbound_events
            WHERE deleted_at IS NULL
            """);
    }
}


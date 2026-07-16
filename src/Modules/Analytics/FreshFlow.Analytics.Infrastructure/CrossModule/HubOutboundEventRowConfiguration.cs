using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class HubOutboundEventRowConfiguration : IEntityTypeConfiguration<HubOutboundEventRow>
{
    public void Configure(EntityTypeBuilder<HubOutboundEventRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT
                id AS "OutboundEventId",
                hub_id AS "HubId",
                destination_route_id AS "DestinationRouteId",
                total_quantity_kg AS "TotalQuantityKg",
                dispatched_at AS "DispatchedAt"
            FROM hub_outbound_events
            WHERE deleted_at IS NULL
            """);
    }
}


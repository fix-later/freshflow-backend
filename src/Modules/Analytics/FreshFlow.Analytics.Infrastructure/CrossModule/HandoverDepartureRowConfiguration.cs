using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class HandoverDepartureRowConfiguration
    : IEntityTypeConfiguration<HandoverDepartureRow>
{
    public void Configure(EntityTypeBuilder<HandoverDepartureRow> builder)
    {
        builder.HasNoKey();

        // One row per route: a route can have several CHECKED_OUT handovers, and joining
        // per-event would multiply every delivery on that route. Earliest confirmation is
        // the departure.
        builder.ToSqlQuery(
            """
            SELECT
                delivery_route_id AS "DeliveryRouteId",
                MIN(driver_confirmed_at) AS "DriverConfirmedAt"
            FROM hub_handover_events
            WHERE status = 'CHECKED_OUT'
              AND driver_confirmed_at IS NOT NULL
              AND deleted_at IS NULL
            GROUP BY delivery_route_id
            """);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class HubSortingStateRowConfiguration
    : IEntityTypeConfiguration<HubSortingStateRow>
{
    public void Configure(EntityTypeBuilder<HubSortingStateRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT
                sorting.route_id     AS "RouteId",
                sorting.hub_id       AS "HubId",
                hub.market_id        AS "MarketId",
                sorting.service_date AS "ServiceDate",
                sorting.status       AS "Status"
            FROM hub_sorting_progress sorting
            INNER JOIN hubs hub ON hub.id = sorting.hub_id AND hub.deleted_at IS NULL
            WHERE sorting.deleted_at IS NULL
            """);
        builder.Property(row => row.RouteId);
        builder.Property(row => row.HubId);
        builder.Property(row => row.MarketId);
        builder.Property(row => row.ServiceDate);
        builder.Property(row => row.Status);
    }
}

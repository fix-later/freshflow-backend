using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class HubCoordinateRowConfiguration : IEntityTypeConfiguration<HubCoordinateRow>
{
    public void Configure(EntityTypeBuilder<HubCoordinateRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT id AS "Id",
                   market_id AS "MarketId",
                   name AS "Name",
                   latitude AS "Latitude",
                   longitude AS "Longitude"
            FROM hubs
            WHERE deleted_at IS NULL AND is_active = TRUE
            """);
    }
}

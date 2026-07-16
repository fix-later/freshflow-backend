using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class DeliveryRowConfiguration : IEntityTypeConfiguration<DeliveryRow>
{
    public void Configure(EntityTypeBuilder<DeliveryRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery(
            """
            SELECT
                id AS "DeliveryId",
                delivery_route_id AS "DeliveryRouteId",
                status AS "Status",
                estimated_arrival AS "EstimatedArrival",
                actual_arrival AS "ActualArrival",
                updated_at AS "UpdatedAt"
            FROM deliveries
            WHERE deleted_at IS NULL
            """);
    }
}


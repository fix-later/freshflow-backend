using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class MarketSessionVehicleAssignmentRowConfiguration
    : IEntityTypeConfiguration<MarketSessionVehicleAssignmentRow>
{
    public void Configure(EntityTypeBuilder<MarketSessionVehicleAssignmentRow> builder)
    {
        builder.HasNoKey();
        builder.ToSqlQuery("""
            SELECT ms.hub_id AS "HubId", ms.service_date AS "ServiceDate", msv.vehicle_id AS "VehicleId"
            FROM market_sessions ms
            JOIN market_session_vehicles msv ON msv.session_id = ms.id
            WHERE ms.hub_id IS NOT NULL AND ms.deleted_at IS NULL
            """);
    }
}

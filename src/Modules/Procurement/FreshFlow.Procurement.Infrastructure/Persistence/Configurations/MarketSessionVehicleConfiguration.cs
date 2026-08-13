using FreshFlow.Procurement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Procurement.Infrastructure.Persistence.Configurations;

internal sealed class MarketSessionVehicleConfiguration : IEntityTypeConfiguration<MarketSessionVehicle>
{
    public void Configure(EntityTypeBuilder<MarketSessionVehicle> builder)
    {
        builder.ToTable("market_session_vehicles");
        builder.HasKey(row => new { row.SessionId, row.VehicleId });
        builder.Property(row => row.SessionId).HasColumnName("session_id");
        builder.Property(row => row.VehicleId).HasColumnName("vehicle_id");
        builder.Property(row => row.AssignedBy).HasColumnName("assigned_by");
        builder.Property(row => row.AssignedAt).HasColumnName("assigned_at");
        builder.HasIndex(row => row.VehicleId);
    }
}

using FreshFlow.Orders.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Orders.Infrastructure.Persistence.Configurations;

internal sealed class OperationalSettingsConfiguration : IEntityTypeConfiguration<OperationalSettings>
{
    public void Configure(EntityTypeBuilder<OperationalSettings> builder)
    {
        builder.ToTable("operational_settings");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(s => s.DailyCutoffTime).HasColumnName("daily_cutoff_time").IsRequired();
        builder.Property(s => s.BatchingEnabled).HasColumnName("batching_enabled").IsRequired();
        builder.Property(s => s.DefaultRouteType).HasColumnName("default_route_type")
            .IsRequired().HasMaxLength(20);
        builder.Property(s => s.DeliveryWindowDays).HasColumnName("delivery_window_days").IsRequired();
        builder.Property(s => s.DeliveryFeePerKm).HasColumnName("delivery_fee_per_km")
            .HasColumnType("numeric(12,2)").IsRequired().HasDefaultValue(5000m);
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        // UpdatedAt is a concurrency token: Touch() bumps it on every Update(), so a concurrent
        // admin edit is detected as a lost update instead of silently overwriting.
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at").IsRequired().IsConcurrencyToken();

        builder.Ignore(s => s.DeletedAt);
        builder.Ignore(s => s.IsDeleted);
    }
}

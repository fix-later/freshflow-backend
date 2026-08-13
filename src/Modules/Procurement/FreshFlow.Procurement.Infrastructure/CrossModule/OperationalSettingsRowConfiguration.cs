using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Procurement.Infrastructure.CrossModule;

internal sealed class OperationalSettingsRowConfiguration : IEntityTypeConfiguration<OperationalSettingsRow>
{
    public void Configure(EntityTypeBuilder<OperationalSettingsRow> builder)
    {
        builder.HasNoKey();
        builder.ToView("operational_settings");
        builder.Property(row => row.BatchingEnabled).HasColumnName("batching_enabled");
        builder.Property(row => row.DailyCutoffTime).HasColumnName("daily_cutoff_time");
        builder.Property(row => row.DeliveryWindowDays).HasColumnName("delivery_window_days");
    }
}

using FreshFlow.Pricing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Pricing.Infrastructure.Persistence.Configurations;

internal sealed class PricingSettingsConfiguration : IEntityTypeConfiguration<PricingSettings>
{
    public void Configure(EntityTypeBuilder<PricingSettings> builder)
    {
        builder.ToTable("pricing_settings");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.PriceAlertThresholdPercent)
            .IsRequired()
            .HasColumnType("numeric(5,2)");
        builder.Property(s => s.CreatedAt).IsRequired();
        // UpdatedAt is a concurrency token: Update() bumps it, so a concurrent admin edit is
        // detected as a lost update instead of silently overwriting.
        builder.Property(s => s.UpdatedAt).IsRequired().IsConcurrencyToken();

        builder.Ignore(s => s.DeletedAt);
        builder.Ignore(s => s.IsDeleted);
    }
}

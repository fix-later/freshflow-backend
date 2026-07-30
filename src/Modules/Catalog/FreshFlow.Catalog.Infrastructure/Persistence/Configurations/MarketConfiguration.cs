using FreshFlow.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Catalog.Infrastructure.Persistence.Configurations;

internal sealed class MarketConfiguration : IEntityTypeConfiguration<Market>
{
    public void Configure(EntityTypeBuilder<Market> builder)
    {
        builder.ToTable("markets");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Name).IsRequired().HasMaxLength(200);
        builder.Property(m => m.Location).HasMaxLength(200);
        builder.Property(m => m.Address).HasMaxLength(500);
        builder.Property(m => m.Latitude).HasColumnType("numeric(9,6)");
        builder.Property(m => m.Longitude).HasColumnType("numeric(9,6)");
        builder.Property(m => m.ImageUrl).HasMaxLength(512);
        builder.Property(m => m.Description).HasMaxLength(2000);
        builder.Property(m => m.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.UpdatedAt).IsRequired();
        builder.Property(m => m.DeletedAt);

        builder.HasIndex(m => m.IsActive).HasDatabaseName("idx_markets_is_active");
        builder.HasIndex(m => m.DeletedAt);
    }
}

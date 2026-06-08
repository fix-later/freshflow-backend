using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Auth.Infrastructure.CrossModule;

internal sealed class DeliveryAddressRowConfiguration : IEntityTypeConfiguration<DeliveryAddressRow>
{
    public void Configure(EntityTypeBuilder<DeliveryAddressRow> builder)
    {
        builder.ToTable("delivery_addresses");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.RestaurantId).IsRequired();
        builder.Property(a => a.RecipientName);
        builder.Property(a => a.Phone).HasMaxLength(20);
        builder.Property(a => a.AddressLine).IsRequired();
        builder.Property(a => a.Latitude).HasPrecision(9, 6);
        builder.Property(a => a.Longitude).HasPrecision(9, 6);
        builder.Property(a => a.IsDefault).IsRequired().HasDefaultValue(false);
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.UpdatedAt).IsRequired();
        builder.Property(a => a.DeletedAt);

        builder.HasOne<RestaurantRow>()
            .WithMany()
            .HasForeignKey(a => a.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Soft-delete query filter — deleted rows are never surfaced by EF queries.
        builder.HasQueryFilter(a => a.DeletedAt == null);

        builder.HasIndex(a => a.RestaurantId);
    }
}

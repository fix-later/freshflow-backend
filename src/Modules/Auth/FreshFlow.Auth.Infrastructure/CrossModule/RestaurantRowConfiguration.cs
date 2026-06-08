using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Auth.Infrastructure.CrossModule;

internal sealed class RestaurantRowConfiguration : IEntityTypeConfiguration<RestaurantRow>
{
    public void Configure(EntityTypeBuilder<RestaurantRow> builder)
    {
        builder.ToTable("restaurants");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Address).HasMaxLength(500);
        builder.Property(r => r.ContactPerson).HasMaxLength(200);
        builder.Property(r => r.PickupStart);
        builder.Property(r => r.PickupEnd);
        builder.Property(r => r.UserId).IsRequired();
        builder.Property(r => r.IsApproved).IsRequired().HasDefaultValue(false);
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt).IsRequired();
        builder.HasIndex(r => r.UserId).IsUnique();
    }
}

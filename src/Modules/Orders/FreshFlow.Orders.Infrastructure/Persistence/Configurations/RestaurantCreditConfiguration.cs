using FreshFlow.Orders.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Orders.Infrastructure.Persistence.Configurations;

internal sealed class RestaurantCreditConfiguration : IEntityTypeConfiguration<RestaurantCredit>
{
    public void Configure(EntityTypeBuilder<RestaurantCredit> builder)
    {
        builder.ToTable("restaurant_credit");
        builder.HasKey(c => c.RestaurantId);

        builder.Property(c => c.RestaurantId)
            .HasColumnName("restaurant_id")
            .IsRequired()
            .ValueGeneratedNever();

        builder.Property(c => c.CreditLimit)
            .HasColumnName("credit_limit")
            .IsRequired()
            .HasColumnType("numeric(14,2)");

        builder.Property(c => c.OutstandingBalance)
            .HasColumnName("outstanding_balance")
            .IsRequired()
            .HasColumnType("numeric(14,2)");

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired()
            .IsConcurrencyToken();

        builder.Ignore(c => c.AvailableCredit);
    }
}

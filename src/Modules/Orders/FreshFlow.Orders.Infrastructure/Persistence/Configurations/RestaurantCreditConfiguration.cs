using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
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

        // SCRUM-266 anti-spam state — single-word enum values, ToLowerInvariant is safe
        // here (unlike PaymentMethod's two-word "bank_transfer", see the 264 lesson).
        builder.Property(c => c.LastAlertedLevel)
            .HasColumnName("last_alerted_level")
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => Enum.Parse<CreditAlertLevel>(v, ignoreCase: true));

        builder.Ignore(c => c.AvailableCredit);

        // RestaurantCredit became an AggregateRoot (DEC-CRE-04) purely to raise domain
        // events — it does NOT use BaseEntity's Id/CreatedAt/DeletedAt at all (its PK is
        // RestaurantId, and restaurant_credit is not a soft-deletable table). Ignore them
        // explicitly so EF doesn't try to map spurious columns for them.
        builder.Ignore(c => c.Id);
        builder.Ignore(c => c.CreatedAt);
        builder.Ignore(c => c.DeletedAt);
        builder.Ignore(c => c.IsDeleted);
    }
}

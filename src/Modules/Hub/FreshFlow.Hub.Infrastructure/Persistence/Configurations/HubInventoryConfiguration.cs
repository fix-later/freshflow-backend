using FreshFlow.Hub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.Infrastructure.Persistence.Configurations;

internal sealed class HubInventoryConfiguration : IEntityTypeConfiguration<HubInventory>
{
    public void Configure(EntityTypeBuilder<HubInventory> builder)
    {
        builder.ToTable("hub_inventory", table =>
        {
            table.HasCheckConstraint("ck_hub_inventory_quantity_in_non_negative", "quantity_in >= 0");
            table.HasCheckConstraint("ck_hub_inventory_quantity_out_non_negative", "quantity_out >= 0");
            table.HasCheckConstraint("ck_hub_inventory_quantity_available_non_negative", "quantity_in >= quantity_out");
        });

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(i => i.HubId)
            .HasColumnName("hub_id")
            .IsRequired();

        builder.Property(i => i.MarketProductId)
            .HasColumnName("market_product_id")
            .IsRequired();

        builder.Property(i => i.QuantityIn)
            .HasColumnName("quantity_in")
            .HasColumnType("numeric(12,2)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(i => i.QuantityOut)
            .HasColumnName("quantity_out")
            .HasColumnType("numeric(12,2)")
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(i => i.QuantityAvailable)
            .HasColumnName("quantity_available")
            .HasColumnType("numeric(12,2)")
            .HasComputedColumnSql("quantity_in - quantity_out", stored: true);

        builder.Property(i => i.RecordedAt)
            .HasColumnName("recorded_at")
            .IsRequired();

        builder.Property(i => i.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(i => i.UpdatedAt)
            .HasColumnName("updated_at")
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(i => i.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasOne<HubEntity>()
            .WithMany()
            .HasForeignKey(i => i.HubId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_hub_inventory_hub");

        builder.HasIndex(i => i.HubId)
            .HasDatabaseName("idx_hub_inventory_hub_id");

        builder.HasIndex(i => i.MarketProductId)
            .HasDatabaseName("idx_hub_inventory_market_product_id");

        builder.HasIndex(i => new { i.HubId, i.MarketProductId })
            .IsUnique()
            .HasDatabaseName("ux_hub_inventory_hub_market_product");
    }
}

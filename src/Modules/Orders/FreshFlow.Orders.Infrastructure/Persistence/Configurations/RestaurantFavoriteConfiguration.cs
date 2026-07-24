using FreshFlow.Orders.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Orders.Infrastructure.Persistence.Configurations;

internal sealed class RestaurantFavoriteConfiguration : IEntityTypeConfiguration<RestaurantFavorite>
{
    public void Configure(EntityTypeBuilder<RestaurantFavorite> builder)
    {
        builder.ToTable("restaurant_favorites");
        builder.HasKey(f => f.Id);

        // Deliberate exception to the "every mutable table has deleted_at" convention: a
        // favorite is a plain on/off toggle, not a record with a lifecycle worth preserving.
        // Hard-deleting on un-favorite is simpler and correct — there is nothing to "resurrect"
        // by soft-deleting, and a soft-deleted row would just have to be un-deleted on the next
        // favorite of the same product anyway.
        builder.Property(f => f.Id).HasColumnName("id").IsRequired();

        // RestaurantId references the Auth module's restaurants table by ID only — no EF
        // navigation/FK declared here (cross-module FKs are DB-constraint-only, see
        // OrderConfiguration.cs). MarketProductId likewise references Pricing's market_products
        // with no FK — matches the plan's explicit "no hard FK" call for both.
        builder.Property(f => f.RestaurantId).HasColumnName("restaurant_id").IsRequired();
        builder.Property(f => f.MarketProductId).HasColumnName("market_product_id").IsRequired();
        builder.Property(f => f.CreatedAt).HasColumnName("created_at").IsRequired();

        // Makes POST idempotent: a second favorite of the same product hits this index instead
        // of creating a duplicate row.
        builder.HasIndex(f => new { f.RestaurantId, f.MarketProductId })
            .IsUnique()
            .HasDatabaseName("ux_restaurant_favorites_restaurant_id_market_product_id");

        builder.HasIndex(f => f.RestaurantId)
            .HasDatabaseName("idx_restaurant_favorites_restaurant_id");
    }
}

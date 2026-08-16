using FreshFlow.Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Auth.Infrastructure.Persistence.Configurations;

internal sealed class UserMarketAssignmentConfiguration : IEntityTypeConfiguration<UserMarketAssignment>
{
    public void Configure(EntityTypeBuilder<UserMarketAssignment> builder)
    {
        builder.ToTable("user_market_assignments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.UserId).IsRequired();
        builder.Property(a => a.MarketId).IsRequired();
        builder.Property(a => a.AssignedBy);
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.UpdatedAt).IsRequired();
        builder.HasIndex(a => new { a.UserId, a.MarketId }).IsUnique();

        builder.HasOne<Domain.Aggregates.User>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Note: the DB FK from market_id → markets.id is preserved as a manual constraint
        // in the CatalogModuleInit migration (not tracked by EF to avoid a cross-module
        // project reference from Auth.Infrastructure → Catalog.Domain).
        // Index on MarketId is also maintained there.
        builder.HasIndex(a => a.MarketId).HasDatabaseName("IX_user_market_assignments_MarketId");
    }
}

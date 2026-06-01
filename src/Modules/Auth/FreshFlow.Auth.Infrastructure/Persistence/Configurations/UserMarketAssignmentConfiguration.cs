using FreshFlow.Auth.Domain.Entities;
using FreshFlow.Auth.Infrastructure.CrossModule;
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

        builder.HasOne<MarketRow>()
            .WithMany()
            .HasForeignKey(a => a.MarketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

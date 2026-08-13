using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Procurement.Infrastructure.Persistence.Configurations;

internal sealed class MarketSessionConfiguration : IEntityTypeConfiguration<MarketSession>
{
    public void Configure(EntityTypeBuilder<MarketSession> builder)
    {
        builder.ToTable("market_sessions");
        builder.HasKey(session => session.Id);

        builder.Property(session => session.Id).HasColumnName("id");
        builder.Property(session => session.MarketId).HasColumnName("market_id").IsRequired();
        builder.Property(session => session.HubId).HasColumnName("hub_id");
        builder.Property(session => session.ServiceDate).HasColumnName("service_date").IsRequired();
        builder.Property(session => session.Status)
            .HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(session => session.ClosesAt).HasColumnName("closes_at").IsRequired();
        builder.Property(session => session.CreatedSource)
            .HasColumnName("created_source").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(session => session.ClosedAt).HasColumnName("closed_at");
        builder.Property(session => session.ClosedBy).HasColumnName("closed_by");
        builder.Property(session => session.CloseReason).HasColumnName("close_reason").HasMaxLength(500);
        builder.Property(session => session.BatchingCompletedAt).HasColumnName("batching_completed_at");
        builder.Property(session => session.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(session => session.UpdatedAt)
            .HasColumnName("updated_at").IsConcurrencyToken().IsRequired();
        builder.Property(session => session.DeletedAt).HasColumnName("deleted_at");

        builder.HasIndex(session => new { session.MarketId, session.ServiceDate })
            .IsUnique()
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("ux_market_sessions_market_date_active");
        builder.HasIndex(session => new { session.Status, session.ClosesAt })
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("idx_market_sessions_status_closes_at");
    }
}

using FreshFlow.Procurement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Procurement.Infrastructure.Persistence.Configurations;

internal sealed class MarketSessionAgentConfiguration : IEntityTypeConfiguration<MarketSessionAgent>
{
    public void Configure(EntityTypeBuilder<MarketSessionAgent> builder)
    {
        builder.ToTable("market_session_agents");
        builder.HasKey(row => new { row.SessionId, row.UserId });
        builder.Property(row => row.SessionId).HasColumnName("session_id");
        builder.Property(row => row.UserId).HasColumnName("user_id");
        builder.Property(row => row.AssignedBy).HasColumnName("assigned_by");
        builder.Property(row => row.AssignedAt).HasColumnName("assigned_at");
        builder.HasIndex(row => row.UserId);
    }
}

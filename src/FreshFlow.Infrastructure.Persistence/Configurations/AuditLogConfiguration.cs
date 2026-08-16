using FreshFlow.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Infrastructure.Persistence.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(a => a.ActorId).HasColumnName("actor_id");
        builder.Property(a => a.Action).HasColumnName("action").IsRequired().HasMaxLength(100);
        builder.Property(a => a.EntityType).HasColumnName("entity_type").IsRequired().HasMaxLength(50);
        builder.Property(a => a.EntityId).HasColumnName("entity_id").IsRequired();
        builder.Property(a => a.Details).HasColumnName("details").HasColumnType("jsonb");
        builder.Property(a => a.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();

        // Filters supported by the admin reader (actor/action/entity/time) — index the ones
        // that narrow a full scan; CreatedAt (time) is the natural ORDER BY so it's indexed too.
        builder.HasIndex(a => a.ActorId);
        builder.HasIndex(a => a.Action);
        builder.HasIndex(a => a.EntityType);
        builder.HasIndex(a => a.CreatedAt);

        // Append-only — no updated_at / deleted_at, same as price_snapshots.
    }
}

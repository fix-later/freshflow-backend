using FreshFlow.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Infrastructure.Persistence.Configurations;

internal sealed class AssistantConversationConfiguration : IEntityTypeConfiguration<AssistantConversation>
{
    public void Configure(EntityTypeBuilder<AssistantConversation> builder)
    {
        builder.ToTable("assistant_conversations");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id")
            .IsRequired()
            .ValueGeneratedNever();

        builder.Property(c => c.SessionId)
            .HasColumnName("session_id")
            .IsRequired()
            .HasMaxLength(128);

        builder.HasIndex(c => c.SessionId).IsUnique();

        builder.Property(c => c.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(c => c.MarketId)
            .HasColumnName("market_id");

        builder.Property(c => c.StateJson)
            .HasColumnName("state")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(c => c.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        // Lazy-expire reads filter on expires_at > now (DbConversationStore), no background sweep —
        // an index keeps that read cheap as the table grows.
        builder.HasIndex(c => c.ExpiresAt);

        // Hard-delete only — no deleted_at / soft-delete query filter for this append/overwrite table.
    }
}

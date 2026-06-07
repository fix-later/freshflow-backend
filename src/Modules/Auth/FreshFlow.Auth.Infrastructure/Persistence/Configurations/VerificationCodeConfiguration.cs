using FreshFlow.Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Auth.Infrastructure.Persistence.Configurations;

internal sealed class VerificationCodeConfiguration : IEntityTypeConfiguration<VerificationCode>
{
    public void Configure(EntityTypeBuilder<VerificationCode> builder)
    {
        builder.ToTable("verification_codes");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.UserId).IsRequired();
        builder.Property(v => v.Channel).IsRequired().HasMaxLength(10);
        builder.Property(v => v.CodeHash).IsRequired().HasMaxLength(128);
        builder.HasIndex(v => v.CodeHash);

        builder.Property(v => v.ExpiresAt).IsRequired();
        builder.Property(v => v.UsedAt);
        builder.Property(v => v.CreatedAt).IsRequired();

        builder.HasOne<Domain.Aggregates.User>()
            .WithMany()
            .HasForeignKey(v => v.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

using FreshFlow.Auth.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Auth.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(255);
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.FullName).HasMaxLength(255);
        builder.Property(u => u.AvatarUrl).HasMaxLength(512);
        builder.Property(u => u.Phone).HasMaxLength(20);
        builder.HasIndex(u => u.Phone).IsUnique().HasFilter("\"Phone\" IS NOT NULL");
        builder.Property(u => u.PasswordHash).IsRequired();

        // FK to roles table — replaces the old string-stored UserRole enum
        builder.Property(u => u.RoleId).IsRequired();
        builder.HasOne(u => u.Role)
               .WithMany()
               .HasForeignKey(u => u.RoleId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.Property(u => u.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(u => u.FailedLoginCount).IsRequired().HasDefaultValue(0);
        builder.Property(u => u.LockedUntil);
        builder.Property(u => u.EmailVerifiedAt);
        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.UpdatedAt).IsRequired();
        builder.Property(u => u.DeletedAt);
        builder.HasQueryFilter(u => u.DeletedAt == null);
    }
}

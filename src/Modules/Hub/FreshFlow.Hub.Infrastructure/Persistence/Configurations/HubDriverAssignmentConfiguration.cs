using FreshFlow.Hub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.Infrastructure.Persistence.Configurations;

internal sealed class HubDriverAssignmentConfiguration
    : IEntityTypeConfiguration<HubDriverAssignment>
{
    public void Configure(EntityTypeBuilder<HubDriverAssignment> builder)
    {
        builder.ToTable("hub_driver_assignments");
        builder.HasKey(assignment => new { assignment.HubId, assignment.UserId });

        builder.Property(assignment => assignment.HubId)
            .HasColumnName("hub_id")
            .IsRequired();

        builder.Property(assignment => assignment.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.HasOne<HubEntity>()
            .WithMany()
            .HasForeignKey(assignment => assignment.HubId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_hub_driver_assignments_hub");

        builder.HasIndex(assignment => assignment.UserId)
            .HasDatabaseName("idx_hub_driver_assignments_user_id");
    }
}

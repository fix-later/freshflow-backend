using FreshFlow.Logistics.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreshFlow.Logistics.Infrastructure.Persistence.Configurations;

internal sealed class RouteMatrixCacheEntryConfiguration : IEntityTypeConfiguration<RouteMatrixCacheEntry>
{
    public void Configure(EntityTypeBuilder<RouteMatrixCacheEntry> builder)
    {
        builder.ToTable("route_matrix_cache");
        builder.HasKey(row => row.PairKey);

        builder.Property(row => row.PairKey)
            .HasColumnName("pair_key")
            .HasColumnType("text");
        builder.Property(row => row.Profile)
            .HasColumnName("profile")
            .HasColumnType("text")
            .IsRequired();
        builder.Property(row => row.FromLatitude)
            .HasColumnName("from_lat")
            .HasColumnType("numeric");
        builder.Property(row => row.FromLongitude)
            .HasColumnName("from_lng")
            .HasColumnType("numeric");
        builder.Property(row => row.ToLatitude)
            .HasColumnName("to_lat")
            .HasColumnType("numeric");
        builder.Property(row => row.ToLongitude)
            .HasColumnName("to_lng")
            .HasColumnType("numeric");
        builder.Property(row => row.DistanceMeters)
            .HasColumnName("distance_meters")
            .HasColumnType("bigint");
        builder.Property(row => row.DurationSeconds)
            .HasColumnName("duration_seconds")
            .HasColumnType("bigint");
        builder.Property(row => row.CalculatedAt)
            .HasColumnName("calculated_at")
            .HasColumnType("timestamp with time zone");
    }
}

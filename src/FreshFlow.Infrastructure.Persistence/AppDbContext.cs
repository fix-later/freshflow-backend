using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Each module's Infrastructure assembly registers its own IEntityTypeConfiguration<T>.
        // Add assemblies here as modules are implemented:
        //   modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuthModuleMarker).Assembly);
    }
}

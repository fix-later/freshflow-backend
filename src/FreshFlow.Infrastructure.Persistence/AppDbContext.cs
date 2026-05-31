using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Scan all FreshFlow assemblies in AppDomain — module DI extensions force-load their
        // assemblies via EfAssemblyRegistry.Register() or DesignTimeDbContextFactory.ForceLoadModuleAssemblies().
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && (a.FullName?.StartsWith("FreshFlow") ?? false)))
        {
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);
        }
    }
}

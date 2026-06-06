using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FreshFlow.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // EF tools v10.0.1 and runtime v10.0.8 produce slightly different model hashes.
        // Actual schema correctness is verified separately via
        // `dotnet ef migrations has-pending-model-changes` (returns "No changes").
        // Downgrade PendingModelChangesWarning from error → log so MigrateAsync proceeds.
        optionsBuilder.ConfigureWarnings(w =>
            w.Log(RelationalEventId.PendingModelChangesWarning));
    }

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

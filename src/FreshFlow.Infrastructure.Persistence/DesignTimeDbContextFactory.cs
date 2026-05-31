using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FreshFlow.Infrastructure.Persistence;

// Used by dotnet-ef at design time (migrations) without needing the full app host.
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Force-load module Infrastructure assemblies so their EF configs are discovered.
        // These DLLs are present in the startup project's bin directory.
        ForceLoadModuleAssemblies();

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(
            Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=freshflow;Username=freshflow;Password=freshflow");

        return new AppDbContext(optionsBuilder.Options);
    }

    private static void ForceLoadModuleAssemblies()
    {
        var moduleAssemblies = new[]
        {
            "FreshFlow.Auth.Infrastructure"
            // Add more as modules are implemented:
            // "FreshFlow.Pricing.Infrastructure",
        };

        foreach (var name in moduleAssemblies)
        {
            try { Assembly.Load(name); }
            catch (FileNotFoundException) { /* Not yet implemented — skip */ }
        }
    }
}

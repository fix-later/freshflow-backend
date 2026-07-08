using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

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
        optionsBuilder.UseNpgsql(ResolveConnectionString());

        return new AppDbContext(optionsBuilder.Options);
    }

    private static string ResolveConnectionString()
    {
        var explicitConnectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(explicitConnectionString))
        {
            return explicitConnectionString;
        }

        var apiConfigDirectory = ResolveApiConfigDirectory();
        var environment =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Development";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiConfigDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        return configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Missing ConnectionStrings:DefaultConnection in FreshFlow.API appsettings.");
    }

    private static string ResolveApiConfigDirectory()
    {
        var candidate = EnumerateConfigDirectoryCandidates()
            .Where(path => File.Exists(Path.Join(path, "appsettings.json")))
            .FirstOrDefault();

        return candidate
            ?? throw new InvalidOperationException(
                "Could not find FreshFlow.API appsettings.json for design-time DbContext creation.");
    }

    private static IEnumerable<string> EnumerateConfigDirectoryCandidates()
    {
        var currentDirectory = Directory.GetCurrentDirectory();

        yield return Path.Join(currentDirectory, "src", "FreshFlow.API");
        yield return currentDirectory;
        yield return AppContext.BaseDirectory;

        for (var directory = new DirectoryInfo(currentDirectory); directory is not null; directory = directory.Parent)
        {
            yield return Path.Join(directory.FullName, "src", "FreshFlow.API");
        }
    }

    private static void ForceLoadModuleAssemblies()
    {
        var moduleAssemblies = new[]
        {
            "FreshFlow.Auth.Infrastructure",
            "FreshFlow.Catalog.Infrastructure",
            "FreshFlow.Pricing.Infrastructure",
            "FreshFlow.Orders.Infrastructure",
            "FreshFlow.Notifications.Infrastructure"
        };

        foreach (var name in moduleAssemblies)
        {
            try { Assembly.Load(name); }
            catch (FileNotFoundException) { /* Not yet implemented — skip */ }
        }
    }
}

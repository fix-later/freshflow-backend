using System.Reflection;

namespace FreshFlow.Infrastructure.Persistence;

/// <summary>
/// Accumulates module Infrastructure assemblies that contain IEntityTypeConfiguration&lt;T&gt; implementations.
/// Each module's AddXxxModule() extension calls Register() so AppDbContext can discover all EF configs.
/// Safe to use as a static because registrations happen in Program.cs before any DbContext is created.
/// </summary>
public static class EfAssemblyRegistry
{
    private static readonly List<Assembly> _assemblies = [typeof(EfAssemblyRegistry).Assembly];

    public static void Register(Assembly assembly)
    {
        if (!_assemblies.Contains(assembly))
            _assemblies.Add(assembly);
    }

    public static IReadOnlyList<Assembly> Assemblies => _assemblies;
}

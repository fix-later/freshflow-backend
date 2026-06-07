using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Domain.Aggregates;
using FreshFlow.Auth.Domain.Entities;
using FreshFlow.Auth.Domain.Enums;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Auth.Infrastructure.Seed;

internal sealed class AdminSeeder(
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ILogger<AdminSeeder> logger) : IHostedService
{
    private static readonly IReadOnlyList<(string Name, string Description)> SeedRoles =
    [
        (RoleNames.Admin, "System administrator with full access"),
        (RoleNames.MarketAgent, "Market agent or kiosk staff managing a market"),
        (RoleNames.Restaurant, "Restaurant owner or operator"),
        (RoleNames.HubStaff, "Hub warehouse and logistics staff"),
        (RoleNames.Driver, "Delivery driver"),
        (RoleNames.OperationsManager, "Operations manager with cross-module visibility")
    ];

    public async Task StartAsync(CancellationToken ct)
    {
        // Env vars take priority; fall back to AdminSeed config section (useful for local dev)
        var email = Environment.GetEnvironmentVariable("ADMIN_SEED_EMAIL")
                    ?? config["AdminSeed:Email"]
                    ?? throw new InvalidOperationException(
                        "Admin seed email required. Set ADMIN_SEED_EMAIL env var or AdminSeed:Email in config.");

        var password = Environment.GetEnvironmentVariable("ADMIN_SEED_PASSWORD")
                       ?? config["AdminSeed:Password"]
                       ?? throw new InvalidOperationException(
                           "Admin seed password required. Set ADMIN_SEED_PASSWORD env var or AdminSeed:Password in config.");

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        await db.Database.MigrateAsync(ct);
        await SeedRolesAsync(db, ct);
        await SeedAdminUserAsync(db, hasher, email, password, ct);
    }

    private async Task SeedRolesAsync(AppDbContext db, CancellationToken ct)
    {
        foreach (var (name, description) in SeedRoles)
        {
            var exists = await db.Set<Role>().AnyAsync(r => r.Name == name, ct);
            if (!exists)
            {
                db.Set<Role>().Add(new Role(name, description));
                logger.LogInformation("[AdminSeeder] Seeding role: {Name}", name);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task SeedAdminUserAsync(AppDbContext db, IPasswordHasher hasher,
        string email, string password, CancellationToken ct)
    {
        var exists = await db.Set<User>()
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == email.ToLowerInvariant(), ct);

        if (exists)
        {
            logger.LogInformation("[AdminSeeder] Admin account already exists, skipping.");
            return;
        }

        var adminRole = await db.Set<Role>().FirstOrDefaultAsync(r => r.Name == RoleNames.Admin, ct)
            ?? throw new InvalidOperationException("Admin role not found after seeding — this should not happen.");

        var admin = User.Create(email, hasher.Hash(password), adminRole);
        db.Set<User>().Add(admin);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("[AdminSeeder] Admin account seeded: {Email}", email);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

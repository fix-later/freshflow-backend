using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Domain.Aggregates;
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

        var exists = await db.Set<User>()
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == email.ToLowerInvariant(), ct);

        if (exists)
        {
            logger.LogInformation("[AdminSeeder] Admin account already exists, skipping.");
            return;
        }

        var admin = User.Create(email, hasher.Hash(password), UserRole.Admin);
        db.Set<User>().Add(admin);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("[AdminSeeder] Admin account seeded: {Email}", email);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Domain.Aggregates;
using FreshFlow.Auth.Domain.Enums;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Auth.Infrastructure.Seed;

internal sealed class AdminSeeder(
    IServiceScopeFactory scopeFactory,
    ILogger<AdminSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var email = Environment.GetEnvironmentVariable("ADMIN_SEED_EMAIL")
                    ?? throw new InvalidOperationException("ADMIN_SEED_EMAIL env var is required.");
        var password = Environment.GetEnvironmentVariable("ADMIN_SEED_PASSWORD")
                       ?? throw new InvalidOperationException("ADMIN_SEED_PASSWORD env var is required.");

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

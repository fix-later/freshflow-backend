using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.IntegrationTests.Infrastructure;

/// <summary>
/// Truncates all Auth-owned tables between tests so each test starts clean.
/// Shared via IClassFixture or ICollectionFixture.
/// </summary>
public sealed class DatabaseResetFixture(AuthWebAppFactory factory)
{
    public async Task ResetAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Disable FK checks, truncate, re-enable
        await db.Database.ExecuteSqlRawAsync("""
            TRUNCATE TABLE refresh_tokens, user_market_assignments, driver_profiles, restaurants, users
            RESTART IDENTITY CASCADE;
            """);

        // Re-seed the Admin account (AdminSeeder runs at startup but truncation removes it)
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO users (id, email, password_hash, role, is_active, created_at, updated_at)
            SELECT gen_random_uuid(), 'admin@test.freshflow',
                   '$2a$12$placeholderHashForTestAdmin', 'admin', true, NOW(), NOW()
            WHERE NOT EXISTS (SELECT 1 FROM users WHERE email = 'admin@test.freshflow');
            """);
    }

    public HttpClient CreateClient() => factory.CreateClient();
}

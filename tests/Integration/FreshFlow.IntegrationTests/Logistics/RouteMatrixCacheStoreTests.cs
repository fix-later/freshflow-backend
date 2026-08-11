using FluentAssertions;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Logistics.Infrastructure.Persistence;
using FreshFlow.Logistics.Infrastructure.Persistence.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.IntegrationTests.Logistics;

[Trait("Category", "Integration")]
public sealed class RouteMatrixCacheStoreTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    [Fact]
    public async Task UpsertThenRead_RoundTripsAndUpdatesAsync()
    {
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IRouteMatrixCacheStore>();
        var key = $"car:{Guid.NewGuid():N}";
        var first = Entry(key, 100, 10, DateTime.UtcNow.AddMinutes(-2));
        var updated = Entry(key, 200, 20, DateTime.UtcNow);

        await store.UpsertAsync([first], default);
        await store.UpsertAsync([updated], default);
        var result = await store.GetAsync([key], DateTime.UtcNow.AddMinutes(-1), default);

        result.Should().ContainSingle();
        result[key].Should().Be((200, 20));
    }

    [Fact]
    public async Task GetAsync_FiltersByKeysAndMaxAgeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IRouteMatrixCacheStore>();
        var freshKey = $"car:{Guid.NewGuid():N}";
        var expiredKey = $"car:{Guid.NewGuid():N}";
        var excludedKey = $"car:{Guid.NewGuid():N}";
        await store.UpsertAsync(
        [
            Entry(freshKey, 100, 10, DateTime.UtcNow),
            Entry(expiredKey, 200, 20, DateTime.UtcNow.AddDays(-31)),
            Entry(excludedKey, 300, 30, DateTime.UtcNow)
        ], default);

        var result = await store.GetAsync(
            [freshKey, expiredKey, $"car:{Guid.NewGuid():N}"],
            DateTime.UtcNow.AddDays(-30),
            default);

        result.Should().ContainSingle().Which.Key.Should().Be(freshKey);
    }

    private static RouteMatrixCacheEntry Entry(
        string key,
        long distanceMeters,
        long durationSeconds,
        DateTime calculatedAt) =>
        new(key, "car", 10m, 106m, 11m, 107m, distanceMeters, durationSeconds, calculatedAt);
}

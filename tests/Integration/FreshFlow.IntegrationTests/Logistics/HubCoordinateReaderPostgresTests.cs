using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Logistics.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.IntegrationTests.Logistics;

/// <summary>
/// Proves the HubCoordinateReader ToSqlQuery projection against real PostgreSQL.
/// </summary>
[Trait("Category", "Integration")]
public sealed class HubCoordinateReaderPostgresTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    [Fact]
    public async Task FindByIdAsync_ActiveHub_ReturnsProjectionAndMissingHubReturnsNullAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var marketId = Guid.NewGuid();
        var hub = HubEntity.Create(
            "Coordinate seam hub", null, 10.75m, 106.67m, 1000m, null, marketId);
        db.Set<HubEntity>().Add(hub);
        await db.SaveChangesAsync();
        var reader = scope.ServiceProvider.GetRequiredService<IHubCoordinateReader>();

        var found = await reader.FindByIdAsync(hub.Id, default);
        var missing = await reader.FindByIdAsync(Guid.NewGuid(), default);

        found.Should().Be(new HubCoordinateDto(hub.Id, marketId, hub.Name, hub.Latitude, hub.Longitude));
        missing.Should().BeNull();
    }
}

using FluentAssertions;
using FreshFlow.Catalog.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Procurement.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FreshFlow.IntegrationTests.Procurement;

[Trait("Category", "Integration")]
public sealed class ProcurementBatchCodePostgresTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    [Fact]
    public async Task MarketCodeReader_ReadsCodeAndNameFromCatalogTableAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var market = new Market(
            $"Chợ Thủ Đức {Guid.NewGuid():N}",
            null,
            null,
            null,
            null,
            code: "TD");
        db.Set<Market>().Add(market);
        await db.SaveChangesAsync();
        var reader = scope.ServiceProvider.GetRequiredService<IMarketCodeReader>();

        var result = await reader.ReadMarketCodesAsync([market.Id], default);

        result.Should().ContainKey(market.Id);
        result[market.Id].Should().Be(("TD", market.Name));
    }

    [Fact]
    public async Task ProcurementBatchCodeUniqueIndex_RejectsDuplicateNonNullCodeAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var code = $"TD-260812-{Guid.NewGuid():N}";
        var batchDate = new DateOnly(2026, 8, 12);
        var marketId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO procurement_batches
                (id, code, batch_date, market_id, status, total_item_count, created_at, updated_at)
            VALUES
                ({Guid.NewGuid()}, {code}, {batchDate}, {marketId}, 'Built', 0, {now}, {now})
            """);

        Func<Task> insertDuplicate = () => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO procurement_batches
                (id, code, batch_date, market_id, status, total_item_count, created_at, updated_at)
            VALUES
                ({Guid.NewGuid()}, {code}, {batchDate}, {marketId}, 'Cancelled', 0, {now}, {now})
            """);

        var exception = await insertDuplicate.Should().ThrowAsync<PostgresException>();

        exception.Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
    }
}

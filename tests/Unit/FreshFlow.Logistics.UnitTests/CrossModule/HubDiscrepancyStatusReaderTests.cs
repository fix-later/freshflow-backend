using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Infrastructure;
using FreshFlow.Logistics.Infrastructure.CrossModule;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Logistics.UnitTests.CrossModule;

[Trait("Category", "Unit")]
public sealed class HubDiscrepancyStatusReaderTests
{
    [Fact]
    public async Task GetOrdersWithOpenDiscrepanciesAsync_ReturnsOnlyOpenNonDeletedMatchesAsync()
    {
        using var fixture = await SqliteFixture.CreateAsync();
        var openOrderId = Guid.NewGuid();
        var acknowledgedOrderId = Guid.NewGuid();
        var deletedOrderId = Guid.NewGuid();
        await fixture.InsertDiscrepancyAsync(openOrderId, "OPEN");
        await fixture.InsertDiscrepancyAsync(acknowledgedOrderId, "ACKNOWLEDGED");
        await fixture.InsertDiscrepancyAsync(deletedOrderId, "OPEN", deleted: true);
        var sut = new HubDiscrepancyStatusReader(fixture.Context);

        var result = await sut.GetOrdersWithOpenDiscrepanciesAsync(
            [openOrderId, acknowledgedOrderId, deletedOrderId, Guid.NewGuid()],
            default);

        result.Should().ContainSingle().Which.Should().Be(openOrderId);
    }

    private sealed class SqliteFixture : IDisposable
    {
        private readonly SqliteConnection _connection;

        private SqliteFixture(SqliteConnection connection, AppDbContext context)
        {
            _connection = connection;
            Context = context;
        }

        public AppDbContext Context { get; }

        public static async Task<SqliteFixture> CreateAsync()
        {
            SQLitePCL.Batteries.Init();
            _ = typeof(FreshFlow.Logistics.Infrastructure.DependencyInjection).Assembly;

            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new AppDbContext(options);

            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE hub_discrepancies (
                    order_id TEXT NOT NULL,
                    status TEXT NOT NULL,
                    deleted_at TEXT NULL
                );
                """);

            return new SqliteFixture(connection, context);
        }

        public async Task InsertDiscrepancyAsync(Guid orderId, string status, bool deleted = false) =>
            await Context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO hub_discrepancies (order_id, status, deleted_at)
                VALUES ({orderId}, {status}, {DeletedAt(deleted)});
                """);

        public void Dispose()
        {
            Context.Dispose();
            _connection.Dispose();
        }

        private static string? DeletedAt(bool deleted) =>
            deleted ? DateTime.UtcNow.ToString("O") : null;
    }
}

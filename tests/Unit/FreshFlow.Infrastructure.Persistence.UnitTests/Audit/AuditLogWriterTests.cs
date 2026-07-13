using FluentAssertions;
using FreshFlow.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FreshFlow.Infrastructure.Persistence.UnitTests.Audit;

[Trait("Category", "Unit")]
public sealed class AuditLogWriterTests
{
    private static AppDbContext CreateInMemoryContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"test-{Guid.NewGuid()}").Options);

    [Fact]
    public async Task WriteAsync_PersistsRowWithAllFields()
    {
        using var db = CreateInMemoryContext();
        var logger = Substitute.For<ILogger<AuditLogWriter>>();
        var sut = new AuditLogWriter(db, logger);
        var actorId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var occurredAt = new DateTime(2026, 6, 18, 7, 30, 0, DateTimeKind.Utc);

        await sut.WriteAsync(actorId, "price_updated", "market_product", entityId, "{}", occurredAt, default);

        var row = await db.Set<AuditLog>().SingleAsync();
        row.ActorId.Should().Be(actorId);
        row.Action.Should().Be("price_updated");
        row.EntityType.Should().Be("market_product");
        row.EntityId.Should().Be(entityId);
        row.Details.Should().Be("{}");
        row.OccurredAt.Should().Be(occurredAt);
    }

    [Fact]
    public async Task WriteAsync_NullActorId_PersistsNull()
    {
        using var db = CreateInMemoryContext();
        var sut = new AuditLogWriter(db, Substitute.For<ILogger<AuditLogWriter>>());

        await sut.WriteAsync(null, "order_cancelled", "order", Guid.NewGuid(), null, DateTime.UtcNow, default);

        var row = await db.Set<AuditLog>().SingleAsync();
        row.ActorId.Should().BeNull();
    }
}

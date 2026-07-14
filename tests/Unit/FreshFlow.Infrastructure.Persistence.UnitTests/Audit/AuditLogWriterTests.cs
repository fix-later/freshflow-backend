using FluentAssertions;
using FreshFlow.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FreshFlow.Infrastructure.Persistence.UnitTests.Audit;

[Trait("Category", "Unit")]
public sealed class AuditLogWriterTests
{
    private static AppDbContext CreateInMemoryContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"test-{Guid.NewGuid()}").Options);

    // AuditLogWriter resolves a fresh AppDbContext from a scope; hand it back the test's context.
    private static IServiceScopeFactory ScopeFactoryFor(AppDbContext db)
    {
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(AppDbContext)).Returns(db);
        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(provider);
        var factory = Substitute.For<IServiceScopeFactory>();
        factory.CreateScope().Returns(scope);
        return factory;
    }

    [Fact]
    public async Task WriteAsync_PersistsRowWithAllFields()
    {
        using var db = CreateInMemoryContext();
        var logger = Substitute.For<ILogger<AuditLogWriter>>();
        var sut = new AuditLogWriter(ScopeFactoryFor(db), logger);
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
        var sut = new AuditLogWriter(ScopeFactoryFor(db), Substitute.For<ILogger<AuditLogWriter>>());

        await sut.WriteAsync(null, "order_cancelled", "order", Guid.NewGuid(), null, DateTime.UtcNow, default);

        var row = await db.Set<AuditLog>().SingleAsync();
        row.ActorId.Should().BeNull();
    }
}

using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Pricing.Domain.Events;
using FreshFlow.SharedKernel.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace FreshFlow.Pricing.UnitTests.Persistence;

/// <summary>
/// Unit tests for <see cref="DomainEventDispatchInterceptor"/>.
///
/// Uses an InMemory EF Core provider with a minimal test DbContext to verify
/// domain event dispatch behavior without any external infrastructure.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DomainEventDispatchInterceptorTests
{
    // ── Infrastructure ────────────────────────────────────────────────────────

    /// <summary>Minimal DbContext wired with the interceptor for test isolation.</summary>
    private sealed class TestDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<TestAggregate> Aggregates => Set<TestAggregate>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestAggregate>(cfg =>
            {
                cfg.HasKey(a => a.Id);
                cfg.Ignore(a => a.DomainEvents); // aggregated in-memory, not persisted
            });
        }
    }

    /// <summary>Minimal aggregate used to raise domain events in tests.</summary>
    private sealed class TestAggregate : AggregateRoot
    {
        public TestAggregate(Guid id) { Id = id; }

        private TestAggregate() { } // EF Core

        public void RaiseEvent(IDomainEvent domainEvent) =>
            RaiseDomainEvent(domainEvent);
    }

    private static DbContextOptions BuildOptions(
        DomainEventDispatchInterceptor interceptor,
        string dbName) =>
        new DbContextOptionsBuilder()
            .UseInMemoryDatabase(dbName)
            .AddInterceptors(interceptor)
            .Options;

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveChangesAsync_WithDomainEvent_PublishesEventPostCommit()
    {
        // Arrange
        var publisher = Substitute.For<IPublisher>();
        var interceptor = new DomainEventDispatchInterceptor(publisher);
        var options = BuildOptions(interceptor, $"db-{Guid.NewGuid()}");

        var evt = new PriceUpdatedDomainEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            100m, 120m, 50, null, DateTime.UtcNow);

        await using var ctx = new TestDbContext(options);
        var agg = new TestAggregate(Guid.NewGuid());
        agg.RaiseEvent(evt);
        await ctx.Aggregates.AddAsync(agg);

        // Act
        await ctx.SaveChangesAsync();

        // Assert — event published exactly once, post-commit
        await publisher.Received(1).Publish(
            Arg.Is<PriceUpdatedDomainEvent>(e => e.MarketProductId == evt.MarketProductId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveChangesAsync_TwoEventsOnOneAggregate_PublishesBoth()
    {
        // Arrange
        var publisher = Substitute.For<IPublisher>();
        var interceptor = new DomainEventDispatchInterceptor(publisher);
        var options = BuildOptions(interceptor, $"db-{Guid.NewGuid()}");

        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        await using var ctx = new TestDbContext(options);
        var agg = new TestAggregate(Guid.NewGuid());
        agg.RaiseEvent(new PriceUpdatedDomainEvent(id1, Guid.NewGuid(), Guid.NewGuid(), 100m, 110m, 50, null, DateTime.UtcNow));
        agg.RaiseEvent(new PriceUpdatedDomainEvent(id2, Guid.NewGuid(), Guid.NewGuid(), 110m, 120m, 50, null, DateTime.UtcNow));
        await ctx.Aggregates.AddAsync(agg);

        // Act
        await ctx.SaveChangesAsync();

        // Assert
        await publisher.Received(2).Publish(Arg.Any<PriceUpdatedDomainEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveChangesAsync_NoDomainEvents_PublishesNothing()
    {
        // Arrange
        var publisher = Substitute.For<IPublisher>();
        var interceptor = new DomainEventDispatchInterceptor(publisher);
        var options = BuildOptions(interceptor, $"db-{Guid.NewGuid()}");

        await using var ctx = new TestDbContext(options);
        var agg = new TestAggregate(Guid.NewGuid()); // no events raised
        await ctx.Aggregates.AddAsync(agg);

        // Act
        await ctx.SaveChangesAsync();

        // Assert
        await publisher.DidNotReceive().Publish(
            Arg.Any<INotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveChangesAsync_ClearsEventsFromAggregate()
    {
        // Arrange
        var publisher = Substitute.For<IPublisher>();
        var interceptor = new DomainEventDispatchInterceptor(publisher);
        var options = BuildOptions(interceptor, $"db-{Guid.NewGuid()}");

        await using var ctx = new TestDbContext(options);
        var agg = new TestAggregate(Guid.NewGuid());
        agg.RaiseEvent(new PriceUpdatedDomainEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, 120m, 50, null, DateTime.UtcNow));
        await ctx.Aggregates.AddAsync(agg);

        // Act
        await ctx.SaveChangesAsync();

        // Assert — events cleared from aggregate; second save does NOT re-publish
        await ctx.SaveChangesAsync();
        await publisher.Received(1).Publish(
            Arg.Any<PriceUpdatedDomainEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveChangesAsync_TwoSeparateSaves_EachPublishesOnce()
    {
        // Arrange — verify events from first save don't bleed into second save
        var publisher = Substitute.For<IPublisher>();
        var interceptor = new DomainEventDispatchInterceptor(publisher);
        var options = BuildOptions(interceptor, $"db-{Guid.NewGuid()}");

        var mpId1 = Guid.NewGuid();
        var mpId2 = Guid.NewGuid();

        await using var ctx = new TestDbContext(options);

        var agg1 = new TestAggregate(Guid.NewGuid());
        agg1.RaiseEvent(new PriceUpdatedDomainEvent(mpId1, Guid.NewGuid(), Guid.NewGuid(), 100m, 110m, 50, null, DateTime.UtcNow));
        await ctx.Aggregates.AddAsync(agg1);
        await ctx.SaveChangesAsync(); // first save → publish mpId1

        var agg2 = new TestAggregate(Guid.NewGuid());
        agg2.RaiseEvent(new PriceUpdatedDomainEvent(mpId2, Guid.NewGuid(), Guid.NewGuid(), 200m, 220m, 30, null, DateTime.UtcNow));
        await ctx.Aggregates.AddAsync(agg2);
        await ctx.SaveChangesAsync(); // second save → publish mpId2 only

        // Assert — each event published exactly once, no bleed
        await publisher.Received(1).Publish(
            Arg.Is<PriceUpdatedDomainEvent>(e => e.MarketProductId == mpId1),
            Arg.Any<CancellationToken>());
        await publisher.Received(1).Publish(
            Arg.Is<PriceUpdatedDomainEvent>(e => e.MarketProductId == mpId2),
            Arg.Any<CancellationToken>());
        await publisher.Received(2).Publish(
            Arg.Any<PriceUpdatedDomainEvent>(), Arg.Any<CancellationToken>());
    }
}

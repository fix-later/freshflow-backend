using FluentAssertions;
using FreshFlow.Infrastructure.Persistence.Audit;
using FreshFlow.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Infrastructure.Persistence.UnitTests.Audit;

[Trait("Category", "Unit")]
public sealed class GetAuditLogsQueryHandlerTests
{
    private static AppDbContext CreateInMemoryContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"test-{Guid.NewGuid()}").Options);

    [Fact]
    public async Task Handle_FiltersByActorActionEntityAndTime()
    {
        using var db = CreateInMemoryContext();
        var actorId = Guid.NewGuid();
        var match = new AuditLog(
            Guid.NewGuid(), actorId, "price_updated", "market_product", Guid.NewGuid(), null,
            new DateTime(2026, 6, 18, 12, 0, 0, DateTimeKind.Utc), DateTime.UtcNow);
        var wrongActor = new AuditLog(
            Guid.NewGuid(), Guid.NewGuid(), "price_updated", "market_product", Guid.NewGuid(), null,
            new DateTime(2026, 6, 18, 12, 0, 0, DateTimeKind.Utc), DateTime.UtcNow);
        var wrongAction = new AuditLog(
            Guid.NewGuid(), actorId, "order_cancelled", "market_product", Guid.NewGuid(), null,
            new DateTime(2026, 6, 18, 12, 0, 0, DateTimeKind.Utc), DateTime.UtcNow);
        var outsideWindow = new AuditLog(
            Guid.NewGuid(), actorId, "price_updated", "market_product", Guid.NewGuid(), null,
            new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc), DateTime.UtcNow);
        db.Set<AuditLog>().AddRange(match, wrongActor, wrongAction, outsideWindow);
        await db.SaveChangesAsync();
        var sut = new GetAuditLogsQueryHandler(db);

        var result = await sut.Handle(
            new GetAuditLogsQuery(
                actorId, "price_updated", "market_product",
                From: new DateTime(2026, 6, 18, 0, 0, 0, DateTimeKind.Utc),
                To: new DateTime(2026, 6, 19, 0, 0, 0, DateTimeKind.Utc)),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Data.Should().ContainSingle(d => d.Id == match.Id);
        result.Value.Total.Should().Be(1);
    }

    [Fact]
    public async Task Handle_NoFilters_ReturnsAllOrderedByOccurredAtDescending()
    {
        using var db = CreateInMemoryContext();
        var older = new AuditLog(
            Guid.NewGuid(), null, "a", "t", Guid.NewGuid(), null,
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), DateTime.UtcNow);
        var newer = new AuditLog(
            Guid.NewGuid(), null, "a", "t", Guid.NewGuid(), null,
            new DateTime(2026, 6, 18, 0, 0, 0, DateTimeKind.Utc), DateTime.UtcNow);
        db.Set<AuditLog>().AddRange(older, newer);
        await db.SaveChangesAsync();
        var sut = new GetAuditLogsQueryHandler(db);

        var result = await sut.Handle(new GetAuditLogsQuery(null, null, null, null, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Data.Should().HaveCount(2);
        result.Value.Data[0].Id.Should().Be(newer.Id);
    }
}

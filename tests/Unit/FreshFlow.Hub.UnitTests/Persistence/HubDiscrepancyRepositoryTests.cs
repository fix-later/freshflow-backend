using FluentAssertions;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.Hub.Infrastructure.Repositories;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Hub.UnitTests.Persistence;

[Trait("Category", "Unit")]
public sealed class HubDiscrepancyRepositoryTests
{
    [Fact]
    public async Task HasOpenDiscrepanciesForOrderAsync_ReturnsTrueOnlyForOpenRowsAsync()
    {
        await using var ctx = CreateContext();
        var repo = new HubDiscrepancyRepository(ctx);
        var orderId = Guid.NewGuid();
        var open = CreateDiscrepancy(orderId);
        var acknowledged = CreateDiscrepancy(orderId);
        acknowledged.Acknowledge(Guid.NewGuid());
        await ctx.Set<HubDiscrepancy>().AddRangeAsync(open, acknowledged);
        await ctx.SaveChangesAsync();

        var hasOpen = await repo.HasOpenDiscrepanciesForOrderAsync(orderId, default);
        var missing = await repo.HasOpenDiscrepanciesForOrderAsync(Guid.NewGuid(), default);

        hasOpen.Should().BeTrue();
        missing.Should().BeFalse();
    }

    private static AppDbContext CreateContext()
    {
        _ = typeof(FreshFlow.Hub.Infrastructure.DependencyInjection).Assembly;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"hub-discrepancy-repo-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    private static HubDiscrepancy CreateDiscrepancy(Guid orderId) =>
        HubDiscrepancy.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            orderId,
            Guid.NewGuid(),
            1m,
            HubDiscrepancy.ConditionMissing,
            null);
}

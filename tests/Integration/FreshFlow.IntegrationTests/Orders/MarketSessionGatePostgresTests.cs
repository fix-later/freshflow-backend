using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.IntegrationTests.Orders;

[Trait("Category", "Integration")]
public sealed class MarketSessionGatePostgresTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    [Fact]
    public async Task ShareLock_BlocksCloseUntilConfirmationTransactionCommitsAsync()
    {
        var marketId = Guid.NewGuid();
        var date = new DateOnly(2026, 8, 20);
        using var confirmationScope = factory.Services.CreateScope();
        var confirmationDb = confirmationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var session = MarketSession.Create(
            marketId,
            Guid.NewGuid(),
            date,
            new DateTime(2026, 8, 19, 15, 0, 0, DateTimeKind.Utc),
            MarketSessionCreatedSource.Manual,
            true).Value;
        confirmationDb.Add(session);
        await confirmationDb.SaveChangesAsync();
        confirmationScope.ServiceProvider.GetRequiredService<IConfiguration>()
            ["Orders:MarketSessions:Enforce"] = "true";

        await using var confirmationTransaction = await confirmationDb.Database.BeginTransactionAsync();
        var gate = confirmationScope.ServiceProvider.GetRequiredService<IMarketSessionGate>();
        (await gate.CheckAsync(marketId, date, true, default)).IsOpen.Should().BeTrue();

        using var closeScope = factory.Services.CreateScope();
        var closeDb = closeScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var closeTask = closeDb.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE market_sessions SET status = 'Closed' WHERE id = {session.Id}");
        (await Task.WhenAny(closeTask, Task.Delay(200))).Should().NotBe(closeTask);

        await confirmationTransaction.CommitAsync();
        (await closeTask).Should().Be(1);
        (await gate.CheckAsync(marketId, date, false, default)).IsOpen.Should().BeFalse();

        confirmationScope.ServiceProvider.GetRequiredService<IConfiguration>()
            ["Orders:MarketSessions:Enforce"] = "false";
        var bypassed = await gate.CheckAsync(marketId, date, false, default);
        bypassed.IsOpen.Should().BeTrue();
        bypassed.SessionId.Should().Be(session.Id);
    }
}

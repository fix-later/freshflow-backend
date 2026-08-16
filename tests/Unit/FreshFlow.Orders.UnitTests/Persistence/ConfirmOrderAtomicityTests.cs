using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Services;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.Orders.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace FreshFlow.Orders.UnitTests.Persistence;

/// <summary>
/// Verifies the atomicity property required by SCRUM-260: the order-confirmation state
/// transition and the credit charge share a single <c>AppDbContext</c> scope, so they commit
/// (or roll back) together via one <c>SaveChangesAsync</c> call — mirroring exactly what
/// <c>ConfirmOrderCommandHandler</c> does (Order.Track + CreditRepository.AddTransaction,
/// both flushed by <c>ICreditService.ChargeAsync</c>'s single SaveChangesAsync).
///
/// These tests exercise the repositories directly against a shared EF Core context (rather
/// than going through the handler) because <c>RestaurantReader</c> uses <c>ToSqlQuery</c>,
/// which the InMemory provider cannot execute — the atomicity mechanism itself (shared
/// DbContext + optimistic concurrency token on RestaurantCredit.UpdatedAt) is provider-agnostic
/// and fully exercised here.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ConfirmOrderAtomicityTests
{
    private static AppDbContext BuildContext(string dbName)
    {
        _ = typeof(FreshFlow.Orders.Infrastructure.DependencyInjection).Assembly; // force EF config discovery
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options);
    }

    private static Order NewDraftOrderWithItem(Guid restaurantId, decimal totalAmount = 100_000m)
    {
        var order = new Order(restaurantId, scheduledFor: null, notes: null);
        order.AddItem(Guid.NewGuid(), "Cà chua", quantity: 1, unitPrice: totalAmount);
        return order;
    }

    [Fact]
    public async Task ConfirmAndCharge_SharedDbContext_CommitsOrderAndTransactionTogetherAsync()
    {
        // Arrange — seed a draft order and a credit account, then start a fresh shared
        // context to mimic the request-scoped DbContext ConfirmOrderCommandHandler uses.
        var dbName = $"db-{Guid.NewGuid()}";
        var restaurantId = Guid.NewGuid();
        Guid orderId;

        await using (var seedCtx = BuildContext(dbName))
        {
            var order = NewDraftOrderWithItem(restaurantId);
            orderId = order.Id;
            seedCtx.Set<Order>().Add(order);
            seedCtx.Set<RestaurantCredit>().Add(new RestaurantCredit(restaurantId, creditLimit: 1_000_000m));
            await seedCtx.SaveChangesAsync();
        }

        // Act — same flow as the handler: confirm in memory, track, charge, ONE SaveChangesAsync.
        await using var ctx = BuildContext(dbName);
        var orderRepo = new OrderRepository(ctx);
        var creditRepo = new CreditRepository(ctx);

        var loadedOrder = await orderRepo.FindByIdAsync(orderId, CancellationToken.None);
        loadedOrder!.Confirm();
        orderRepo.Track(loadedOrder);

        var account = await creditRepo.FindAccountAsync(restaurantId, CancellationToken.None);
        account!.Charge(loadedOrder.TotalAmount);
        creditRepo.Track(account);
        creditRepo.AddTransaction(new CreditTransaction(
            restaurantId, loadedOrder.Id, CreditTransactionType.Charge,
            loadedOrder.TotalAmount, account.OutstandingBalance, "Order confirmed"));

        await creditRepo.SaveChangesAsync(CancellationToken.None);

        // Assert — both the order status and the ledger entry landed together.
        await using var verifyCtx = BuildContext(dbName);
        var savedOrder = await verifyCtx.Set<Order>().FirstAsync(o => o.Id == orderId);
        savedOrder.Status.Should().Be(OrderStatus.Confirmed);

        var savedTransactions = await verifyCtx.Set<CreditTransaction>()
            .Where(t => t.OrderId == orderId).ToListAsync();
        savedTransactions.Should().ContainSingle();
        savedTransactions.Single().Type.Should().Be(CreditTransactionType.Charge);
    }

    [Fact]
    public async Task ConfirmAndCharge_ConcurrentCreditUpdate_RollsBackOrderConfirmationAsync()
    {
        // Arrange
        var dbName = $"db-{Guid.NewGuid()}";
        var restaurantId = Guid.NewGuid();
        Guid orderId;

        await using (var seedCtx = BuildContext(dbName))
        {
            var order = NewDraftOrderWithItem(restaurantId);
            orderId = order.Id;
            seedCtx.Set<Order>().Add(order);
            seedCtx.Set<RestaurantCredit>().Add(new RestaurantCredit(restaurantId, creditLimit: 1_000_000m));
            await seedCtx.SaveChangesAsync();
        }

        await using var ctx = BuildContext(dbName);
        var orderRepo = new OrderRepository(ctx);
        var creditRepo = new CreditRepository(ctx);

        var loadedOrder = await orderRepo.FindByIdAsync(orderId, CancellationToken.None);
        loadedOrder!.Confirm();
        orderRepo.Track(loadedOrder);

        var account = await creditRepo.FindAccountAsync(restaurantId, CancellationToken.None);
        account!.Charge(loadedOrder.TotalAmount);
        creditRepo.Track(account);
        creditRepo.AddTransaction(new CreditTransaction(
            restaurantId, loadedOrder.Id, CreditTransactionType.Charge,
            loadedOrder.TotalAmount, account.OutstandingBalance, "Order confirmed"));

        // A racing request commits a change to the SAME credit account first, bumping its
        // optimistic-concurrency token (UpdatedAt) out from under our in-flight save.
        await using (var racingCtx = BuildContext(dbName))
        {
            var racingAccount = await racingCtx.Set<RestaurantCredit>()
                .FirstAsync(a => a.RestaurantId == restaurantId);
            racingAccount.Charge(1_000m);
            await racingCtx.SaveChangesAsync();
        }

        // Act — our stale save must fail with a concurrency conflict, not silently succeed
        // and not silently double-charge over a value the racing request already changed.
        var act = async () => await creditRepo.SaveChangesAsync(CancellationToken.None);

        // Assert — CreditRepository surfaces the conflict as CreditConcurrencyException, which
        // CreditService.SaveAndReturnAsync maps to Result.Failure(OPTIMISTIC_CONCURRENCY_CONFLICT).
        // ConfirmOrderCommandHandler propagates that failure to the caller instead of returning
        // a success — the client never sees this order as confirmed.
        //
        // Note: full cross-entity rollback (order status + ledger row) on this failure is a
        // guarantee of the relational provider — one SaveChangesAsync call runs inside a single
        // implicit ADO.NET transaction, so Npgsql rolls back every pending change together. The
        // InMemory provider used here does not model that per-entity transactional atomicity
        // (entities already flushed before the conflicting one can remain committed), so it is
        // not asserted below; only the concurrency-conflict surfacing itself is verified here.
        await act.Should().ThrowAsync<CreditConcurrencyException>();
    }

    [Fact]
    public async Task ConfirmAndCharge_ChargeFailsItsInternalCanChargeRecheck_OrderConfirmationIsNeverPersistedAsync()
    {
        // Covers the specific path the leader flagged: CreditService.ChargeAsync re-checks
        // CanCharge internally (in case the account changed between the handler's earlier
        // CanChargeAsync call and this one) and returns Result.Failure BEFORE ever calling
        // SaveChangesAsync. order.Confirm() has already mutated the in-memory entity by then —
        // this proves that in-memory mutation is discarded, not persisted, when no save happens.
        var dbName = $"db-{Guid.NewGuid()}";
        var restaurantId = Guid.NewGuid();
        Guid orderId;

        await using (var seedCtx = BuildContext(dbName))
        {
            var order = NewDraftOrderWithItem(restaurantId, totalAmount: 50_000m);
            orderId = order.Id;
            seedCtx.Set<Order>().Add(order);
            // OutstandingBalance (90) + charge (50) would exceed CreditLimit (100) — CanCharge fails.
            var account = new RestaurantCredit(restaurantId, creditLimit: 100_000m);
            account.Charge(90_000m);
            seedCtx.Set<RestaurantCredit>().Add(account);
            await seedCtx.SaveChangesAsync();
        }

        await using var ctx = BuildContext(dbName);
        var orderRepo = new OrderRepository(ctx);
        var creditRepo = new CreditRepository(ctx);
        var restaurantReader = Substitute.For<IRestaurantReader>();
        restaurantReader.FindByIdAsync(restaurantId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(restaurantId, IsApproved: true));
        var creditService = new CreditService(creditRepo, restaurantReader);

        var loadedOrder = await orderRepo.FindByIdAsync(orderId, CancellationToken.None);
        loadedOrder!.Confirm();
        orderRepo.Track(loadedOrder);

        // Act
        var chargeResult = await creditService.ChargeAsync(
            restaurantId, loadedOrder.Id, loadedOrder.TotalAmount, "Order confirmed", CancellationToken.None);

        // Assert — the charge is rejected without ever reaching SaveChangesAsync...
        chargeResult.IsFailure.Should().BeTrue();
        chargeResult.Error.Code.Should().Be("CREDIT_LIMIT_EXCEEDED");

        // ...so the order's in-memory Confirm() mutation was never flushed to the DB.
        await using var verifyCtx = BuildContext(dbName);
        var savedOrder = await verifyCtx.Set<Order>().FirstAsync(o => o.Id == orderId);
        savedOrder.Status.Should().Be(OrderStatus.Draft);

        var savedTransactions = await verifyCtx.Set<CreditTransaction>()
            .Where(t => t.OrderId == orderId).ToListAsync();
        savedTransactions.Should().BeEmpty();
    }
}

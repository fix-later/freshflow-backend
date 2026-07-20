using FluentAssertions;
using FreshFlow.Contracts;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Services;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using MediatR;
using NSubstitute;

namespace FreshFlow.Orders.UnitTests.Services;

[Trait("Category", "Unit")]
public sealed class CreditStatementGenerationServiceTests
{
    private readonly ICreditStatementRepository _statementRepository = Substitute.For<ICreditStatementRepository>();
    private readonly ICreditRepository _creditRepository = Substitute.For<ICreditRepository>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly CreditStatementGenerationService _sut;

    private static readonly Guid RestaurantId = Guid.NewGuid();

    // A safely closed period well before "now" — every test that doesn't specifically
    // exercise the not-yet-closed guard uses this.
    private const int ClosedYear = 2020;
    private const int ClosedMonth = 6;

    public CreditStatementGenerationServiceTests()
    {
        _statementRepository.FindByPeriodAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns((CreditStatement?)null);
        _creditRepository.GetTransactionsInPeriodAsync(
                Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<CreditTransaction>());
        _creditRepository.GetNetBalanceMovementBeforeAsync(
                Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(0m);

        _sut = new CreditStatementGenerationService(_statementRepository, _creditRepository, _publisher);
    }

    [Fact]
    public async Task GenerateAsync_PeriodNotYetClosed_ReturnsValidationErrorAsync()
    {
        var future = DateTime.UtcNow.AddMonths(2);

        var result = await _sut.GenerateAsync(RestaurantId, future.Year, future.Month, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("STATEMENT_PERIOD_NOT_CLOSED");
        await _statementRepository.DidNotReceive().AddAsync(Arg.Any<CreditStatement>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_CurrentIncompleteMonth_ReturnsValidationErrorAsync()
    {
        // "Not yet closed" must reject the CURRENT month too, not just strictly-future
        // ones — a statement is an immutable snapshot, so freezing a partial month would
        // be wrong even though the current month is not, strictly speaking, "future".
        var now = DateTime.UtcNow;

        var result = await _sut.GenerateAsync(RestaurantId, now.Year, now.Month, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("STATEMENT_PERIOD_NOT_CLOSED");
    }

    [Fact]
    public async Task GenerateAsync_StatementAlreadyExistsForPeriod_ReturnsExistingWithoutRebuildingAsync()
    {
        var existing = new CreditStatement(
            RestaurantId,
            new DateTime(2020, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2020, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            0m, 0m, 0m, 0m, []);
        _statementRepository.FindByPeriodAsync(RestaurantId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(existing);

        var result = await _sut.GenerateAsync(RestaurantId, ClosedYear, ClosedMonth, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(existing.Id);
        await _statementRepository.DidNotReceive().AddAsync(Arg.Any<CreditStatement>(), Arg.Any<CancellationToken>());
        await _creditRepository.DidNotReceive().GetTransactionsInPeriodAsync(
            Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_AlwaysSourcesOpeningBalanceFromTheLedgerAsync()
    {
        // DEC-CRE-06: opening balance ALWAYS comes from GetNetBalanceMovementBeforeAsync —
        // there is no statement-chaining shortcut, so this holds regardless of whether any
        // prior statement exists. (The gap-scenario regression proving this is safe even
        // when a period was skipped lives in CreditStatementGenerationGapScenarioTests,
        // which exercises the real repositories end-to-end.)
        _creditRepository.GetNetBalanceMovementBeforeAsync(
                RestaurantId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(75m);

        var result = await _sut.GenerateAsync(RestaurantId, ClosedYear, ClosedMonth, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.OpeningBalance.Should().Be(75m);
        result.Value.ClosingBalance.Should().Be(75m);
        await _creditRepository.Received(1).GetNetBalanceMovementBeforeAsync(
            RestaurantId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_AggregatesTotalsAndSnapshotsLinesFromPeriodTransactionsAsync()
    {
        var transactions = new[]
        {
            new CreditTransaction(RestaurantId, Guid.NewGuid(), CreditTransactionType.Charge, 100m, 100m, "Order A"),
            new CreditTransaction(RestaurantId, Guid.NewGuid(), CreditTransactionType.Charge, 50m, 150m, "Order B"),
            new CreditTransaction(
                RestaurantId, null, CreditTransactionType.Settlement, 60m, 90m, "Paid",
                PaymentMethod.BankTransfer, "TXN-1"),
            new CreditTransaction(RestaurantId, Guid.NewGuid(), CreditTransactionType.Refund, 10m, 80m, "Cancelled"),
        };
        _creditRepository.GetTransactionsInPeriodAsync(
                RestaurantId, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(transactions);

        var result = await _sut.GenerateAsync(RestaurantId, ClosedYear, ClosedMonth, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCharges.Should().Be(150m);
        result.Value.TotalSettlements.Should().Be(60m);
        result.Value.TotalRefunds.Should().Be(10m);
        result.Value.ClosingBalance.Should().Be(80m); // 0 + 150 - 60 - 10
        result.Value.Lines.Should().HaveCount(4);
        result.Value.Lines.Should().Contain(l => l.Reference == "TXN-1" && l.Type == "settlement");
    }

    [Fact]
    public async Task GenerateAsync_ConcurrentGenerateWinsTheRace_ReturnsTheRacedStatementAsync()
    {
        var raced = new CreditStatement(
            RestaurantId,
            new DateTime(2020, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2020, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            0m, 0m, 0m, 0m, []);
        _statementRepository.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new CreditStatementConcurrencyException()));
        _statementRepository.FindByPeriodAsync(RestaurantId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns((CreditStatement?)null, raced);

        var result = await _sut.GenerateAsync(RestaurantId, ClosedYear, ClosedMonth, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(raced.Id);
        // The race winner already published — the loser must not re-notify.
        await _publisher.DidNotReceive().Publish(
            Arg.Any<CreditStatementGeneratedIntegrationEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_ConcurrencyConflictWithNoRacedStatementFound_ReturnsConflictAsync()
    {
        _statementRepository.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new CreditStatementConcurrencyException()));

        var result = await _sut.GenerateAsync(RestaurantId, ClosedYear, ClosedMonth, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("STATEMENT_GENERATION_CONFLICT");
    }

    [Fact]
    public async Task GenerateAsync_NewlyGenerated_PublishesStatementGeneratedEventAsync()
    {
        var result = await _sut.GenerateAsync(RestaurantId, ClosedYear, ClosedMonth, default);

        result.IsSuccess.Should().BeTrue();
        await _publisher.Received(1).Publish(
            Arg.Is<CreditStatementGeneratedIntegrationEvent>(e =>
                e.RestaurantId == RestaurantId && e.StatementId == result.Value.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_IdempotentReturnOfExistingStatement_DoesNotPublishAsync()
    {
        var existing = new CreditStatement(
            RestaurantId,
            new DateTime(2020, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2020, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            0m, 0m, 0m, 0m, []);
        _statementRepository.FindByPeriodAsync(RestaurantId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(existing);

        await _sut.GenerateAsync(RestaurantId, ClosedYear, ClosedMonth, default);

        await _publisher.DidNotReceive().Publish(
            Arg.Any<CreditStatementGeneratedIntegrationEvent>(), Arg.Any<CancellationToken>());
    }
}

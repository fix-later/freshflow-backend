using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Services;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using NSubstitute;

namespace FreshFlow.Orders.UnitTests.Services;

[Trait("Category", "Unit")]
public sealed class CreditServiceTests
{
    private static readonly Guid AdminId = Guid.NewGuid();
    private readonly ICreditRepository _creditRepository = Substitute.For<ICreditRepository>();
    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();
    private readonly CreditService _sut;

    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    public CreditServiceTests()
    {
        _restaurantReader.FindByIdAsync(RestaurantId, default)
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
        _creditRepository.GetRefundableAmountForOrderAsync(OrderId, default)
            .Returns(100m);
        _sut = new CreditService(_creditRepository, _restaurantReader);
    }

    [Fact]
    public async Task CanCharge_InvalidAmount_ReturnsInvalidAmountWithoutReadingRestaurantAsync()
    {
        var result = await _sut.CanChargeAsync(RestaurantId, 0m, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_AMOUNT");
        await _restaurantReader.DidNotReceive().FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CanCharge_RestaurantMissing_ReturnsNotFoundAsync()
    {
        _restaurantReader.FindByIdAsync(RestaurantId, default)
            .Returns((RestaurantSnapshotDto?)null);

        var result = await _sut.CanChargeAsync(RestaurantId, 10m, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }

    [Fact]
    public async Task CanCharge_AmountExceedsAvailableCredit_ReturnsCreditLimitExceededAsync()
    {
        var account = new RestaurantCredit(RestaurantId, creditLimit: 100m);
        account.Charge(80m);
        _creditRepository.FindAccountAsync(RestaurantId, default).Returns(account);

        var result = await _sut.CanChargeAsync(RestaurantId, 25m, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CREDIT_LIMIT_EXCEEDED");
        await _creditRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CanCharge_WithinLimit_ReturnsCreditCheckAsync()
    {
        var account = new RestaurantCredit(RestaurantId, creditLimit: 100m);
        account.Charge(20m);
        _creditRepository.FindAccountAsync(RestaurantId, default).Returns(account);

        var result = await _sut.CanChargeAsync(RestaurantId, 50m, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.CanCharge.Should().BeTrue();
        result.Value.AvailableCredit.Should().Be(80m);
        result.Value.RequestedAmount.Should().Be(50m);
    }

    [Fact]
    public async Task Charge_WithinLimit_UpdatesBalanceAndWritesLedgerAsync()
    {
        var account = new RestaurantCredit(RestaurantId, creditLimit: 100m);
        _creditRepository.FindAccountAsync(RestaurantId, default).Returns(account);

        var result = await _sut.ChargeAsync(RestaurantId, OrderId, 40m, "Order confirmed", default);

        result.IsSuccess.Should().BeTrue();
        result.Value.OutstandingBalance.Should().Be(40m);
        _creditRepository.Received(1).Track(account);
        _creditRepository.Received(1).AddTransaction(Arg.Is<CreditTransaction>(t =>
            t.RestaurantId == RestaurantId
            && t.OrderId == OrderId
            && t.Type == CreditTransactionType.Charge
            && t.Amount == 40m
            && t.BalanceAfter == 40m));
        await _creditRepository.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Charge_SaveConcurrencyConflict_ReturnsOptimisticConcurrencyConflictAsync()
    {
        var account = new RestaurantCredit(RestaurantId, creditLimit: 100m);
        _creditRepository.FindAccountAsync(RestaurantId, default).Returns(account);
        _creditRepository.SaveChangesAsync(default).Returns(Task.FromException(new CreditConcurrencyException()));

        var result = await _sut.ChargeAsync(RestaurantId, OrderId, 40m, "Order confirmed", default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("OPTIMISTIC_CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task Settle_ExceedingOutstandingBalance_ReturnsSettlementExceededAsync()
    {
        var account = new RestaurantCredit(RestaurantId, creditLimit: 100m);
        account.Charge(30m);
        _creditRepository.FindAccountAsync(RestaurantId, default).Returns(account);

        var result = await _sut.SettleAsync(
            RestaurantId, AdminId, 31m, PaymentMethod.BankTransfer, "TXN-1", "Bank transfer", default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CREDIT_SETTLEMENT_EXCEEDS_BALANCE");
        await _creditRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Settle_WithinOutstandingBalance_DecreasesBalanceAndWritesLedgerAsync()
    {
        var account = new RestaurantCredit(RestaurantId, creditLimit: 100m);
        account.Charge(70m);
        _creditRepository.FindAccountAsync(RestaurantId, default).Returns(account);

        var result = await _sut.SettleAsync(
            RestaurantId, AdminId, 25m, PaymentMethod.BankTransfer, " txn-42 ", "Bank transfer", default);

        result.IsSuccess.Should().BeTrue();
        result.Value.OutstandingBalance.Should().Be(45m);
        _creditRepository.Received(1).AddTransaction(Arg.Is<CreditTransaction>(t =>
            t.RestaurantId == RestaurantId
            && t.OrderId == null
            && t.Type == CreditTransactionType.Settlement
            && t.Amount == 25m
            && t.BalanceAfter == 45m
            && t.PaymentMethod == PaymentMethod.BankTransfer
            && t.Reference == "TXN-42"
            && t.RecordedByUserId == AdminId));
        await _creditRepository.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Settle_WithoutReference_ReturnsValidationAsync()
    {
        var account = new RestaurantCredit(RestaurantId, creditLimit: 100m);
        account.Charge(50m);
        _creditRepository.FindAccountAsync(RestaurantId, default).Returns(account);

        var result = await _sut.SettleAsync(
            RestaurantId, AdminId, 20m, PaymentMethod.Manual, reference: null, note: null, ct: default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_SETTLEMENT_DETAILS");
        _creditRepository.DidNotReceive().AddTransaction(Arg.Any<CreditTransaction>());
    }

    [Fact]
    public async Task Settle_DuplicateReference_ReturnsConflictAsync()
    {
        var account = new RestaurantCredit(RestaurantId, creditLimit: 100m);
        account.Charge(50m);
        _creditRepository.FindAccountAsync(RestaurantId, default).Returns(account);
        _creditRepository.SaveChangesAsync(default)
            .Returns(Task.FromException(
                new DuplicateCreditSettlementException(new InvalidOperationException("duplicate"))));

        var result = await _sut.SettleAsync(
            RestaurantId, AdminId, 20m, PaymentMethod.BankTransfer, "TXN-1", null, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CREDIT_SETTLEMENT_DUPLICATE_REFERENCE");
    }

    [Fact]
    public async Task Refund_WithinOutstandingBalance_DecreasesBalanceAndWritesLedgerAsync()
    {
        var account = new RestaurantCredit(RestaurantId, creditLimit: 100m);
        account.Charge(60m);
        _creditRepository.FindAccountAsync(RestaurantId, default).Returns(account);

        var result = await _sut.RefundAsync(RestaurantId, OrderId, 10m, "Order cancelled", default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Credit.OutstandingBalance.Should().Be(50m);
        _creditRepository.Received(1).AddTransaction(Arg.Is<CreditTransaction>(t =>
            t.RestaurantId == RestaurantId
            && t.OrderId == OrderId
            && t.Type == CreditTransactionType.Refund
            && t.Amount == 10m
            && t.BalanceAfter == 50m));
        await _creditRepository.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Refund_ExceedsRemainingOrderCharge_ReturnsValidationAsync()
    {
        var account = new RestaurantCredit(RestaurantId, creditLimit: 200m);
        account.Charge(100m);
        _creditRepository.FindAccountAsync(RestaurantId, default).Returns(account);
        _creditRepository.GetRefundableAmountForOrderAsync(OrderId, default).Returns(5m);

        var result = await _sut.RefundAsync(RestaurantId, OrderId, 10m, null, default);

        result.Error.Code.Should().Be("CREDIT_REFUND_EXCEEDS_ORDER_CHARGE");
        _creditRepository.DidNotReceive().AddTransaction(Arg.Any<CreditTransaction>());

        await _creditRepository.DidNotReceive().SaveChangesAsync(default);
    }
    [Fact]
    public async Task SetCreditLimit_NewRestaurant_CreatesAccountWithoutTouchingBalanceLedgerAsync()
    {
        _creditRepository.FindAccountAsync(RestaurantId, default).Returns((RestaurantCredit?)null);

        var result = await _sut.SetCreditLimitAsync(RestaurantId, 1000m, "Initial limit", default);

        result.IsSuccess.Should().BeTrue();
        result.Value.CreditLimit.Should().Be(1000m);
        await _creditRepository.Received(1).AddAccountAsync(
            Arg.Is<RestaurantCredit>(a => a.RestaurantId == RestaurantId), default);

        // A limit change is not a balance movement — no CreditTransaction row is written.
        _creditRepository.DidNotReceive().AddTransaction(Arg.Any<CreditTransaction>());
        await _creditRepository.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task SetCreditLimit_ExistingAccountIncreaseLimit_UpdatesLimitWithoutTouchingBalanceLedgerAsync()
    {
        var account = new RestaurantCredit(RestaurantId, creditLimit: 100m);
        account.Charge(40m);
        _creditRepository.FindAccountAsync(RestaurantId, default).Returns(account);

        var result = await _sut.SetCreditLimitAsync(RestaurantId, 300m, "Trusted partner", default);

        result.IsSuccess.Should().BeTrue();
        result.Value.CreditLimit.Should().Be(300m);

        // A limit change is not a balance movement — no CreditTransaction row is written.
        _creditRepository.DidNotReceive().AddTransaction(Arg.Any<CreditTransaction>());
        await _creditRepository.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task SetCreditLimit_NoChangeFromCurrentLimit_SkipsLedgerAndSaveAsync()
    {
        var account = new RestaurantCredit(RestaurantId, creditLimit: 100m);
        _creditRepository.FindAccountAsync(RestaurantId, default).Returns(account);

        var result = await _sut.SetCreditLimitAsync(RestaurantId, 100m, null, default);

        result.IsSuccess.Should().BeTrue();
        _creditRepository.DidNotReceive().AddTransaction(Arg.Any<CreditTransaction>());
        await _creditRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetCreditLimit_BelowOutstandingBalance_ReturnsValidationErrorAsync()
    {
        var account = new RestaurantCredit(RestaurantId, creditLimit: 100m);
        account.Charge(80m);
        _creditRepository.FindAccountAsync(RestaurantId, default).Returns(account);

        var result = await _sut.SetCreditLimitAsync(RestaurantId, 50m, null, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CREDIT_LIMIT_BELOW_OUTSTANDING_BALANCE");
        await _creditRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetCreditLimit_NegativeValue_ReturnsValidationErrorAsync()
    {
        var result = await _sut.SetCreditLimitAsync(RestaurantId, -1m, null, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_CREDIT_LIMIT");
        await _restaurantReader.DidNotReceive().FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetCreditLimit_RestaurantMissing_ReturnsNotFoundAsync()
    {
        _restaurantReader.FindByIdAsync(RestaurantId, default)
            .Returns((RestaurantSnapshotDto?)null);

        var result = await _sut.SetCreditLimitAsync(RestaurantId, 500m, null, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }
}

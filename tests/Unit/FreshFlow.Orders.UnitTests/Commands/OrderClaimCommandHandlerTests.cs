using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Commands.ApproveClaim;
using FreshFlow.Orders.Application.Commands.FileClaim;
using FreshFlow.Orders.Application.Commands.RejectClaim;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using NSubstitute;

namespace FreshFlow.Orders.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class OrderClaimCommandHandlerTests
{
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();
    private readonly IOrderClaimRepository _claims = Substitute.For<IOrderClaimRepository>();
    private readonly IRestaurantReader _restaurants = Substitute.For<IRestaurantReader>();
    private readonly ICreditService _credit = Substitute.For<ICreditService>();

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();

    public OrderClaimCommandHandlerTests()
    {
        _orders.ExecuteInSerializableTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<Result>>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<Func<CancellationToken, Task<Result>>>(0)(
                call.ArgAt<CancellationToken>(1)));
        _restaurants.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));
    }

    [Fact]
    public async Task FileClaim_OwnerClaimableOrder_PersistsSubmittedClaimAsync()
    {
        var order = NewAtHubOrder(RestaurantId);
        _orders.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        var sut = new FileClaimCommandHandler(_orders, _claims, _restaurants);

        var result = await sut.Handle(
            new FileClaimCommand(UserId, order.Id, 50_000m, "Damaged produce"),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("submitted");
        result.Value.RestaurantId.Should().Be(RestaurantId);
        await _claims.Received(1).AddAsync(
            Arg.Is<OrderClaim>(claim =>
                claim.OrderId == order.Id
                && claim.RestaurantId == RestaurantId
                && claim.Amount == 50_000m
                && claim.CreatedBy == UserId),
            Arg.Any<CancellationToken>());
        await _claims.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FileClaim_WithProofImageUrl_PersistsItOnTheClaimAsync()
    {
        var order = NewAtHubOrder(RestaurantId);
        _orders.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        var sut = new FileClaimCommandHandler(_orders, _claims, _restaurants);

        var result = await sut.Handle(
            new FileClaimCommand(
                UserId, order.Id, 50_000m, "Damaged produce", "https://res.cloudinary.com/proof.jpg"),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.ProofImageUrl.Should().Be("https://res.cloudinary.com/proof.jpg");
        await _claims.Received(1).AddAsync(
            Arg.Is<OrderClaim>(claim => claim.ProofImageUrl == "https://res.cloudinary.com/proof.jpg"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FileClaim_OtherRestaurantsOrder_ReturnsForbiddenAsync()
    {
        var order = NewAtHubOrder(Guid.NewGuid());
        _orders.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        var sut = new FileClaimCommandHandler(_orders, _claims, _restaurants);

        var result = await sut.Handle(
            new FileClaimCommand(UserId, order.Id, 10_000m, "Wrong order"),
            default);

        result.Error.Code.Should().Be("FORBIDDEN");
        await _claims.DidNotReceive().AddAsync(
            Arg.Any<OrderClaim>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FileClaim_AmountExceedsOrderCharge_ReturnsValidationAsync()
    {
        var order = NewAtHubOrder(RestaurantId);
        _orders.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        var sut = new FileClaimCommandHandler(_orders, _claims, _restaurants);

        var result = await sut.Handle(
            new FileClaimCommand(UserId, order.Id, order.TotalAmount + 1m, "Too much"),
            default);

        result.Error.Code.Should().Be("INVALID_CLAIM_AMOUNT");
        await _claims.DidNotReceive().AddAsync(
            Arg.Any<OrderClaim>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FileClaim_NonClaimableOrder_ReturnsConflictAsync()
    {
        var order = new Order(RestaurantId, null, null);
        order.AddItem(Guid.NewGuid(), "Tomato", 1, 100_000m);
        order.Confirm();
        _orders.FindByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        var sut = new FileClaimCommandHandler(_orders, _claims, _restaurants);

        var result = await sut.Handle(
            new FileClaimCommand(UserId, order.Id, 10_000m, "Too early"),
            default);

        result.Error.Code.Should().Be("CLAIM_ORDER_NOT_CLAIMABLE");
    }

    [Fact]
    public async Task ApproveClaim_RefundsOnceAndRecordsTransactionIdAsync()
    {
        var claim = NewClaim();
        var transactionId = Guid.NewGuid();
        _claims.FindByIdAsync(claim.Id, Arg.Any<CancellationToken>()).Returns(claim);
        _credit.RefundAsync(
                RestaurantId, claim.OrderId, claim.Amount, Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<CreditRefundDto>.Success(
                new CreditRefundDto(
                    transactionId,
                    new RestaurantCreditDto(RestaurantId, 200_000m, 50_000m, 150_000m, DateTime.UtcNow))));
        var sut = new ApproveClaimCommandHandler(_orders, _claims, _credit);
        var command = new ApproveClaimCommand(UserId, claim.Id, "Verified");

        var first = await sut.Handle(command, default);
        var second = await sut.Handle(command, default);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        claim.Status.Should().Be(OrderClaimStatus.Approved);
        claim.RefundTransactionId.Should().Be(transactionId);
        await _credit.Received(1).RefundAsync(
            RestaurantId, claim.OrderId, claim.Amount, "Claim approved: Verified", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApproveClaim_RefundFailure_LeavesClaimSubmittedAsync()
    {
        var claim = NewClaim();
        _claims.FindByIdAsync(claim.Id, Arg.Any<CancellationToken>()).Returns(claim);
        _credit.RefundAsync(
                RestaurantId, claim.OrderId, claim.Amount, Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result<CreditRefundDto>.Failure(
                Error.Validation("CREDIT_REFUND_EXCEEDS_BALANCE", "Refund failed.")));
        var sut = new ApproveClaimCommandHandler(_orders, _claims, _credit);

        var result = await sut.Handle(
            new ApproveClaimCommand(UserId, claim.Id, null),
            default);

        result.Error.Code.Should().Be("CREDIT_REFUND_EXCEEDS_BALANCE");
        claim.Status.Should().Be(OrderClaimStatus.Submitted);
        _claims.DidNotReceive().Track(claim);
    }

    [Fact]
    public async Task RejectClaim_RecordsReviewAndDoesNotRefundAsync()
    {
        var claim = NewClaim();
        _claims.FindByIdAsync(claim.Id, Arg.Any<CancellationToken>()).Returns(claim);
        var sut = new RejectClaimCommandHandler(_orders, _claims);

        var result = await sut.Handle(
            new RejectClaimCommand(UserId, claim.Id, "Insufficient evidence"),
            default);

        result.IsSuccess.Should().BeTrue();
        claim.Status.Should().Be(OrderClaimStatus.Rejected);
        claim.ReviewedBy.Should().Be(UserId);
        claim.DecisionNote.Should().Be("Insufficient evidence");
        _claims.Received(1).Track(claim);
        await _credit.DidNotReceive().RefundAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    private static OrderClaim NewClaim() =>
        new(
            Guid.NewGuid(),
            RestaurantId,
            50_000m,
            "Damaged produce",
            UserId,
            new DateTime(2026, 8, 4, 1, 0, 0, DateTimeKind.Utc));

    private static Order NewAtHubOrder(Guid restaurantId)
    {
        var order = new Order(restaurantId, null, null);
        order.AddItem(Guid.NewGuid(), "Tomato", 1, 100_000m);
        order.Confirm();
        order.AdvanceStatus(OrderStatus.Batched);
        order.AdvanceStatus(OrderStatus.PickedUp);
        order.AdvanceStatus(OrderStatus.AtHub);
        return order;
    }
}

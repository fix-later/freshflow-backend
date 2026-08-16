using FluentAssertions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Application.Services;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;

namespace FreshFlow.Orders.UnitTests.Services;

/// <summary>
/// ASSIST-E3-T1 — <see cref="OrderConfirmationEvaluator"/> pure-evaluation unit tests.
/// Issue ordering MUST mirror the legacy <c>ConfirmOrderCommandHandler</c> check order
/// (CanConfirm → credit → window) so <c>Issues[0]</c> equals the legacy first-error
/// (regression-guarded by ASSIST-E3-T2).
/// </summary>
[Trait("Category", "Unit")]
public sealed class OrderConfirmationEvaluatorTests
{
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid MarketProductId = Guid.NewGuid();

    private static Order NewDraftOrderWithItem(DateTime? scheduledFor = null) =>
        WithItem(new Order(RestaurantId, scheduledFor, notes: null));

    private static Order WithItem(Order order)
    {
        order.AddItem(MarketProductId, "Cà chua", 5, 20_000m);
        return order;
    }

    private static CreditCheckDto SuccessfulCreditCheck(decimal requestedAmount) =>
        new(RestaurantId, CreditLimit: 1_000_000m, OutstandingBalance: 0m,
            AvailableCredit: 1_000_000m, requestedAmount, CanCharge: true);

    [Fact]
    public void Evaluate_DraftOrderWithinWindow_ReturnsNoIssues()
    {
        var confirmedAtUtc = new DateTime(2026, 6, 18, 10, 0, 0, DateTimeKind.Utc);
        var order = NewDraftOrderWithItem(scheduledFor: confirmedAtUtc.AddDays(2));
        var creditCheck = SuccessfulCreditCheck(order.TotalAmount);

        var evaluation = OrderConfirmationEvaluator.Evaluate(order, creditCheck, confirmedAtUtc, windowDays: 7);

        evaluation.Issues.Should().BeEmpty();
        evaluation.TotalAmount.Should().Be(order.TotalAmount);
        evaluation.CreditCheck.Should().Be(creditCheck);
    }

    [Fact]
    public void Evaluate_NonDraftOrder_ReturnsOrderNotDraftAsFirstIssue()
    {
        var order = NewDraftOrderWithItem();
        order.Confirm();
        var confirmedAtUtc = new DateTime(2026, 6, 18, 10, 0, 0, DateTimeKind.Utc);
        var creditCheck = SuccessfulCreditCheck(order.TotalAmount);

        var evaluation = OrderConfirmationEvaluator.Evaluate(order, creditCheck, confirmedAtUtc, windowDays: 7);

        evaluation.Issues.Should().NotBeEmpty();
        evaluation.Issues[0].Code.Should().Be("ORDER_NOT_DRAFT");
    }

    [Fact]
    public void Evaluate_EmptyOrder_ReturnsOrderEmptyAsFirstIssue()
    {
        var order = new Order(RestaurantId, scheduledFor: null, notes: null);
        var confirmedAtUtc = new DateTime(2026, 6, 18, 10, 0, 0, DateTimeKind.Utc);
        var creditCheck = SuccessfulCreditCheck(order.TotalAmount);

        var evaluation = OrderConfirmationEvaluator.Evaluate(order, creditCheck, confirmedAtUtc, windowDays: 7);

        evaluation.Issues.Should().NotBeEmpty();
        evaluation.Issues[0].Code.Should().Be("ORDER_EMPTY");
    }

    [Fact]
    public void Evaluate_ScheduledForBeyondDPlus7_ReturnsDeliveryDateOutOfWindowIssue()
    {
        var confirmedAtUtc = new DateTime(2026, 6, 18, 10, 0, 0, DateTimeKind.Utc);
        var order = NewDraftOrderWithItem(scheduledFor: confirmedAtUtc.AddDays(8));
        var creditCheck = SuccessfulCreditCheck(order.TotalAmount);

        var evaluation = OrderConfirmationEvaluator.Evaluate(order, creditCheck, confirmedAtUtc, windowDays: 7);

        evaluation.Issues.Should().NotBeEmpty();
        evaluation.Issues[0].Code.Should().Be("DELIVERY_DATE_OUT_OF_WINDOW");
    }

    [Fact]
    public void Evaluate_ScheduledForBeyondDPlus7WithConfiguredFourteenDayWindow_ReturnsNoIssues()
    {
        // Same date that Evaluate_ScheduledForBeyondDPlus7 rejects under the default 7-day
        // window is admitted once the admin-configured window (SCRUM-179) is 14 days.
        var confirmedAtUtc = new DateTime(2026, 6, 18, 10, 0, 0, DateTimeKind.Utc);
        var order = NewDraftOrderWithItem(scheduledFor: confirmedAtUtc.AddDays(8));
        var creditCheck = SuccessfulCreditCheck(order.TotalAmount);

        var evaluation = OrderConfirmationEvaluator.Evaluate(order, creditCheck, confirmedAtUtc, windowDays: 14);

        evaluation.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_PastCutoffWithNoRequestedDate_ResolvesScheduledForToNextDeliveryCycle()
    {
        // Confirm at 23:00 Vietnam time (16:00 UTC) — past the 22:00 cutoff.
        var confirmedAtUtc = new DateTime(2026, 6, 18, 16, 0, 0, DateTimeKind.Utc);
        var order = NewDraftOrderWithItem(scheduledFor: null);
        var creditCheck = SuccessfulCreditCheck(order.TotalAmount);

        var evaluation = OrderConfirmationEvaluator.Evaluate(order, creditCheck, confirmedAtUtc, windowDays: 7);

        evaluation.Issues.Should().BeEmpty();
        evaluation.ResolvedScheduledFor.Should().NotBeNull();
        evaluation.ResolvedScheduledFor!.Value.Should().BeAfter(confirmedAtUtc);
    }

    [Fact]
    public void Evaluate_NonDraftOrderBeyondWindow_ReturnsOrderNotDraftFirst_NotWindowIssue()
    {
        // CanConfirm is checked before the window — Issues[0] must be ORDER_NOT_DRAFT
        // even though the schedule is also out of window, matching legacy short-circuit order.
        var confirmedAtUtc = new DateTime(2026, 6, 18, 10, 0, 0, DateTimeKind.Utc);
        var order = NewDraftOrderWithItem(scheduledFor: confirmedAtUtc.AddDays(8));
        order.Confirm();
        var creditCheck = SuccessfulCreditCheck(order.TotalAmount);

        var evaluation = OrderConfirmationEvaluator.Evaluate(order, creditCheck, confirmedAtUtc, windowDays: 7);

        evaluation.Issues[0].Code.Should().Be("ORDER_NOT_DRAFT");
    }

    [Fact]
    public void Evaluate_DoesNotMutateOrder()
    {
        var confirmedAtUtc = new DateTime(2026, 6, 18, 10, 0, 0, DateTimeKind.Utc);
        var order = NewDraftOrderWithItem(scheduledFor: null);
        var creditCheck = SuccessfulCreditCheck(order.TotalAmount);

        OrderConfirmationEvaluator.Evaluate(order, creditCheck, confirmedAtUtc, windowDays: 7);

        order.Status.Should().Be(OrderStatus.Draft);
        order.ScheduledFor.Should().BeNull();
    }
}

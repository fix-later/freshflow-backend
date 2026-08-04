using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Services;

/// <summary>
/// Pure, non-mutating evaluation of whether an order is confirmable (ASSIST-E3-T1).
/// Gathers the same verdicts <see cref="Commands.ConfirmOrder.ConfirmOrderCommandHandler"/>
/// checks before mutating state, in the same order, so <c>Issues[0]</c> always equals the
/// legacy short-circuit's first error: CanConfirm → credit → delivery window.
///
/// The caller is responsible for calling <c>ICreditService.CanChargeAsync</c> and
/// short-circuiting on <c>Result.Failure</c> before invoking this method — over-limit credit
/// is represented as <c>Result.Failure(CREDIT_LIMIT_EXCEEDED)</c>, never as a <see cref="CreditCheckDto"/>
/// (confirmed in ASSIST-E3-T0). The <paramref name="creditCheck"/> passed in is therefore always
/// the success-path DTO and never itself contributes an issue here; preview callers that need to
/// surface a credit-limit failure as an issue (rather than a system error) adapt it separately.
/// </summary>
internal static class OrderConfirmationEvaluator
{
    public static OrderConfirmationEvaluation Evaluate(
        Order order,
        CreditCheckDto creditCheck,
        DateTime nowUtc,
        int windowDays,
        TimeSpan? cutoffLocalTime = null,
        decimal? totalAmount = null)
    {
        var issues = new List<Error>();

        var canConfirmResult = order.CanConfirm();
        if (canConfirmResult.IsFailure)
            issues.Add(canConfirmResult.Error);

        // Credit is already verified successful by the caller (see remarks above) — no issue
        // can originate from creditCheck itself, but it's threaded through for the preview's
        // RemainingCreditAfter calculation.

        var resolvedScheduledFor = OrderCutoffScheduler.ResolveScheduledFor(nowUtc, order.ScheduledFor, cutoffLocalTime);
        if (resolvedScheduledFor is not null
            && !OrderCutoffScheduler.IsWithinDeliveryWindow(nowUtc, resolvedScheduledFor.Value, windowDays))
        {
            issues.Add(Error.Validation(
                "DELIVERY_DATE_OUT_OF_WINDOW",
                $"Delivery date must be within the next {windowDays} days and not in the past."));
        }

        return new OrderConfirmationEvaluation(
            issues, resolvedScheduledFor, totalAmount ?? order.TotalAmount, creditCheck);
    }
}

public sealed record OrderConfirmationEvaluation(
    IReadOnlyList<Error> Issues,
    DateTime? ResolvedScheduledFor,
    decimal TotalAmount,
    CreditCheckDto CreditCheck);

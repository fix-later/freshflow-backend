using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Application.Services;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Queries.PreviewOrderConfirmation;

internal sealed class PreviewOrderConfirmationQueryHandler(
    IOrderRepository orderRepository,
    IRestaurantReader restaurantReader,
    ICreditService creditService,
    IOperationalSettingsRepository operationalSettings)
    : IRequestHandler<PreviewOrderConfirmationQuery, Result<OrderConfirmationPreviewDto>>
{
    public Task<Result<OrderConfirmationPreviewDto>> Handle(
        PreviewOrderConfirmationQuery request, CancellationToken cancellationToken) =>
        Handle(request, cancellationToken, DateTime.UtcNow);

    internal async Task<Result<OrderConfirmationPreviewDto>> Handle(
        PreviewOrderConfirmationQuery request, CancellationToken cancellationToken, DateTime nowUtc)
    {
        var order = await orderRepository.FindByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result<OrderConfirmationPreviewDto>.Failure(Error.NotFound("ORDER", request.OrderId));

        var restaurant = await restaurantReader.FindByUserIdAsync(request.UserId, cancellationToken);
        if (restaurant is null || restaurant.RestaurantId != order.RestaurantId)
            return Result<OrderConfirmationPreviewDto>.Failure(
                Error.Unauthorized("FORBIDDEN", "This order does not belong to the authenticated restaurant."));

        var canChargeResult = await creditService.CanChargeAsync(
            order.RestaurantId, order.TotalAmount, cancellationToken);
        if (canChargeResult.IsFailure)
        {
            // Unlike confirm, a credit-limit failure is a displayable preview issue, not a
            // query failure — the caller still gets a 200 with WouldSucceed=false. There is no
            // CreditCheckDto on this path, so RemainingCreditAfter stays null rather than
            // triggering an extra repo read.
            return Result<OrderConfirmationPreviewDto>.Success(new OrderConfirmationPreviewDto(
                WouldSucceed: false,
                Issues: [new PreviewIssueDto(canChargeResult.Error.Code, canChargeResult.Error.Message)],
                TotalAmount: order.TotalAmount,
                ResolvedScheduledFor: null,
                RemainingCreditAfter: null));
        }

        var settings = await operationalSettings.GetAsync(cancellationToken);
        var evaluation = OrderConfirmationEvaluator.Evaluate(
            order, canChargeResult.Value, nowUtc, settings.DeliveryWindowDays, settings.DailyCutoffTime.ToTimeSpan());
        var issues = evaluation.Issues
            .Select(error => new PreviewIssueDto(error.Code, error.Message))
            .ToList();

        return Result<OrderConfirmationPreviewDto>.Success(new OrderConfirmationPreviewDto(
            WouldSucceed: issues.Count == 0,
            Issues: issues,
            TotalAmount: evaluation.TotalAmount,
            ResolvedScheduledFor: evaluation.ResolvedScheduledFor,
            RemainingCreditAfter: evaluation.CreditCheck.AvailableCredit - evaluation.TotalAmount));
    }
}

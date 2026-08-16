using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Application.Services;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Commands.ReportOrderIssue;

internal sealed class ReportOrderIssueCommandHandler(
    IOrderRepository orderRepository,
    IOrderIssueRepository orderIssueRepository,
    IRestaurantReader restaurantReader)
    : IRequestHandler<ReportOrderIssueCommand, Result<OrderIssueDto>>
{
    public async Task<Result<OrderIssueDto>> Handle(
        ReportOrderIssueCommand request, CancellationToken cancellationToken)
    {
        var order = await orderRepository.FindByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result<OrderIssueDto>.Failure(Error.NotFound("ORDER", request.OrderId));

        var restaurant = await restaurantReader.FindByUserIdAsync(request.UserId, cancellationToken);
        if (restaurant is null || restaurant.RestaurantId != order.RestaurantId)
            return Result<OrderIssueDto>.Failure(
                Error.Unauthorized("FORBIDDEN", "This order does not belong to the authenticated restaurant."));

        if (order.Status != OrderStatus.Delivered)
            return Result<OrderIssueDto>.Failure(Error.Conflict(
                "ORDER_ISSUE_NOT_ALLOWED", "Issues can only be reported for delivered orders."));

        if (!OrderIssueParsing.TryParseIssueType(request.IssueType, out var issueType))
            return Result<OrderIssueDto>.Failure(Error.Validation(
                "VALIDATION_ERROR", "IssueType must be 'missing', 'wrong', or 'damaged'."));

        if (request.AffectedQuantity <= 0m)
            return Result<OrderIssueDto>.Failure(Error.Validation(
                "INVALID_ISSUE_QUANTITY", "AffectedQuantity must be greater than zero."));

        if (string.IsNullOrWhiteSpace(request.Description)
            || request.Description.Trim().Length > OrderIssue.MaxDescriptionLength)
        {
            return Result<OrderIssueDto>.Failure(Error.Validation(
                "VALIDATION_ERROR", $"Description must be 1-{OrderIssue.MaxDescriptionLength} characters."));
        }

        if (request.OrderItemId.HasValue)
        {
            var item = order.Items.FirstOrDefault(i => i.Id == request.OrderItemId.Value);
            if (item is null)
                return Result<OrderIssueDto>.Failure(Error.NotFound("ORDER_ITEM", request.OrderItemId.Value));

            if (request.AffectedQuantity > item.Quantity)
                return Result<OrderIssueDto>.Failure(Error.Validation(
                    "INVALID_ISSUE_QUANTITY", "AffectedQuantity cannot exceed the ordered item quantity."));
        }

        var issue = new OrderIssue(
            order.Id,
            request.OrderItemId,
            request.UserId,
            issueType,
            request.AffectedQuantity,
            request.Description);

        await orderIssueRepository.AddAsync(issue, cancellationToken);
        await orderIssueRepository.SaveChangesAsync(cancellationToken);

        return Result<OrderIssueDto>.Success(OrderIssueDtoMapper.ToDto(issue));
    }
}

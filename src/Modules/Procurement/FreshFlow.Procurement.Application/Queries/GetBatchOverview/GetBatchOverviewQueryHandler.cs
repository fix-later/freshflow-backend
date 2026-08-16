using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Procurement.Application.Queries.GetBatchOverview;

internal sealed class GetBatchOverviewQueryHandler(
    IProcurementBatchRepository batches,
    IConfirmedOrderReader orders)
    : IRequestHandler<GetBatchOverviewQuery, Result<ProcurementBatchOverviewDto>>
{
    public async Task<Result<ProcurementBatchOverviewDto>> Handle(
        GetBatchOverviewQuery request,
        CancellationToken cancellationToken)
    {
        var batch = await batches.FindByIdAsync(request.BatchId, cancellationToken);
        if (batch is null)
        {
            return Result<ProcurementBatchOverviewDto>.Failure(
                Error.NotFound("PROCUREMENT_BATCH", request.BatchId));
        }

        var orderIds = batch.Orders.Select(order => order.OrderId).ToArray();
        var orderStatuses = await orders.ReadStatusesAsync(orderIds, cancellationToken);
        var itemCosts = await orders.ReadItemCostsAsync(orderIds, cancellationToken);
        var itemsPurchased = batch.Items.Count(item => item.ActualQuantity is not null);
        var isCancelled = batch.Status == ProcurementBatchStatus.Cancelled;
        var costSummary = ProcurementCostSummaryCalculator.Calculate(
            batch.Items,
            orderIds,
            itemCosts);

        return Result<ProcurementBatchOverviewDto>.Success(new(
            batch.Id,
            batch.Code,
            batch.MarketId,
            batch.HubId,
            batch.BatchDate,
            batch.Status.ToString(),
            batch.ManifestedAt,
            batch.HandedOffAt,
            batch.CompletedAt,
            batch.CancelledAt,
            batch.CancellationReason,
            batch.TotalItemCount,
            itemsPurchased,
            isCancelled ? 0 : batch.Items.Count - itemsPurchased,
            batch.Exceptions.Count(exception => !exception.IsDeleted),
            costSummary.RestaurantOrderTotal,
            costSummary.ActualPurchaseTotal,
            batch.Items
                .Where(item => item.AssignedAgentUserId is not null)
                .GroupBy(item => item.AssignedAgentUserId!.Value)
                .Select(group => MapAgent(batch, group, orderIds, itemCosts, isCancelled))
                .ToList()
                .AsReadOnly(),
            orderIds
                .Select(orderId => new BatchOrderStatusDto(
                    orderId,
                    orderStatuses.GetValueOrDefault(orderId)))
                .ToList()
                .AsReadOnly()));
    }

    private static BatchAgentPerformanceDto MapAgent(
        ProcurementBatch batch,
        IGrouping<Guid, ProcurementBatchItem> group,
        IReadOnlyCollection<Guid> orderIds,
        IReadOnlyCollection<ConfirmedOrderItemCostDto> itemCosts,
        bool isCancelled)
    {
        var items = group.ToList();
        var purchased = items.Where(item => item.ActualQuantity is not null).ToList();
        var allSettled = items.All(item =>
            item.ActualQuantity is not null || batch.Exceptions.Any(exception =>
                !exception.IsDeleted &&
                exception.Type == ProcurementExceptionType.Unavailable &&
                exception.MarketProductId == item.MarketProductId));
        decimal? referenceCost = items.All(item => item.ReferenceUnitPrice is not null)
            ? items.Sum(item => item.ReferenceUnitPrice!.Value *
                (item.ActualQuantity ?? item.TotalQuantity))
            : null;
        decimal? actualCost = allSettled && purchased.All(item => item.ActualUnitPrice is not null)
            ? purchased.Sum(item => item.ActualUnitPrice!.Value * item.ActualQuantity!.Value)
            : null;
        var costSummary = ProcurementCostSummaryCalculator.Calculate(items, orderIds, itemCosts);

        return new BatchAgentPerformanceDto(
            group.Key,
            items.Count,
            purchased.Count,
            isCancelled ? 0 : items.Count - purchased.Count,
            batch.Exceptions.Count(exception =>
                !exception.IsDeleted && exception.ReportedByUserId == group.Key),
            costSummary.RestaurantOrderTotal,
            costSummary.ActualPurchaseTotal,
            referenceCost,
            actualCost,
            referenceCost is not null && actualCost is not null
                ? actualCost - referenceCost
                : null);
    }
}

using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Procurement.Application.Queries.GetAssignedProcurementTask;

internal sealed class GetAssignedProcurementTaskQueryHandler(
    IProcurementBatchRepository batches,
    IConfirmedOrderReader orders,
    IMarketProductImageReader images)
    : IRequestHandler<GetAssignedProcurementTaskQuery, Result<ProcurementBatchDto>>
{
    public async Task<Result<ProcurementBatchDto>> Handle(
        GetAssignedProcurementTaskQuery request,
        CancellationToken cancellationToken)
    {
        var batch = await batches.FindByIdAsync(request.BatchId, cancellationToken);
        if (batch is null ||
            !batch.Items.Any(item => item.AssignedAgentUserId == request.AgentUserId))
        {
            return Result<ProcurementBatchDto>.Failure(
                Error.NotFound("PROCUREMENT_BATCH", request.BatchId));
        }

        var orderIds = batch.Orders
            .Select(link => link.OrderId)
            .ToArray();
        var statuses = await orders.ReadStatusesAsync(orderIds, cancellationToken);
        var restaurantNames = await orders.ReadRestaurantNamesAsync(orderIds, cancellationToken);
        var marketProductIds = batch.Items
            .Select(item => item.MarketProductId)
            .ToArray();
        var productInfo = await images.ReadInfoAsync(marketProductIds, cancellationToken);
        var itemCosts = await orders.ReadItemCostsAsync(orderIds, cancellationToken);
        var costSummary = ProcurementCostSummaryCalculator.Calculate(
            batch.Items.Where(item => item.AssignedAgentUserId == request.AgentUserId),
            orderIds,
            itemCosts);

        return Result<ProcurementBatchDto>.Success(
            ProcurementBatchDtoMapper.Map(
                batch,
                statuses,
                productInfo,
                costSummary,
                restaurantNames));
    }
}

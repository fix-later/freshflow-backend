using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Procurement.Application.Queries.GetAssignedProcurementTasks;

internal sealed class GetAssignedProcurementTasksQueryHandler(
    IProcurementBatchRepository batches,
    IConfirmedOrderReader orders,
    IMarketProductImageReader images)
    : IRequestHandler<GetAssignedProcurementTasksQuery, Result<ProcurementBatchListDto>>
{
    public async Task<Result<ProcurementBatchListDto>> Handle(
        GetAssignedProcurementTasksQuery request,
        CancellationToken cancellationToken)
    {
        var (page, total) = await batches.ListByAgentAsync(
            request.AgentUserId,
            request.Page,
            request.PageSize,
            cancellationToken);
        var orderIds = page
            .SelectMany(batch => batch.Orders)
            .Select(link => link.OrderId)
            .Distinct()
            .ToArray();
        var statuses = await orders.ReadStatusesAsync(orderIds, cancellationToken);
        var marketProductIds = page
            .SelectMany(batch => batch.Items)
            .Select(item => item.MarketProductId)
            .Distinct()
            .ToArray();
        var imageUrls = await images.ReadImagesAsync(marketProductIds, cancellationToken);
        var itemCosts = await orders.ReadItemCostsAsync(orderIds, cancellationToken);

        return Result<ProcurementBatchListDto>.Success(
            new ProcurementBatchListDto(
                page.Select(batch => ProcurementBatchDtoMapper.Map(
                        batch,
                        statuses,
                        imageUrls,
                        ProcurementCostSummaryCalculator.Calculate(
                            batch.Items.Where(item =>
                                item.AssignedAgentUserId == request.AgentUserId),
                            batch.Orders.Select(order => order.OrderId),
                            itemCosts)))
                    .ToList()
                    .AsReadOnly(),
                new ProcurementBatchPaginationDto(
                    total,
                    request.Page,
                    request.PageSize)));
    }
}

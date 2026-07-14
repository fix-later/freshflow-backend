using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Procurement.Application.Queries.GetProcurementBatches;

internal sealed class GetProcurementBatchesQueryHandler(
    IProcurementBatchRepository batches,
    IConfirmedOrderReader orders)
    : IRequestHandler<GetProcurementBatchesQuery, Result<ProcurementBatchListDto>>
{
    public async Task<Result<ProcurementBatchListDto>> Handle(
        GetProcurementBatchesQuery request,
        CancellationToken cancellationToken)
    {
        var (page, total) = await batches.ListAsync(
            request.Page,
            request.PageSize,
            cancellationToken);
        var orderIds = page
            .SelectMany(batch => batch.Orders)
            .Select(link => link.OrderId)
            .Distinct()
            .ToArray();
        var statuses = await orders.ReadStatusesAsync(orderIds, cancellationToken);

        return Result<ProcurementBatchListDto>.Success(
            new ProcurementBatchListDto(
                page.Select(batch => ProcurementBatchDtoMapper.Map(batch, statuses))
                    .ToList()
                    .AsReadOnly(),
                new ProcurementBatchPaginationDto(
                    total,
                    request.Page,
                    request.PageSize)));
    }
}

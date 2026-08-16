using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Procurement.Application.Queries.GetProcurementBatch;

public sealed record GetProcurementBatchQuery(Guid BatchId) : IQuery<ProcurementBatchDto>;

internal sealed class GetProcurementBatchQueryHandler(
    IProcurementBatchRepository batches,
    IConfirmedOrderReader orders)
    : IRequestHandler<GetProcurementBatchQuery, Result<ProcurementBatchDto>>
{
    public async Task<Result<ProcurementBatchDto>> Handle(
        GetProcurementBatchQuery request,
        CancellationToken cancellationToken)
    {
        var batch = await batches.FindByIdAsync(request.BatchId, cancellationToken);
        if (batch is null)
        {
            return Result<ProcurementBatchDto>.Failure(
                Error.NotFound("PROCUREMENT_BATCH", request.BatchId));
        }

        var orderIds = batch.Orders.Select(link => link.OrderId).ToArray();
        var statuses = await orders.ReadStatusesAsync(orderIds, cancellationToken);

        return Result<ProcurementBatchDto>.Success(
            ProcurementBatchDtoMapper.Map(batch, statuses));
    }
}

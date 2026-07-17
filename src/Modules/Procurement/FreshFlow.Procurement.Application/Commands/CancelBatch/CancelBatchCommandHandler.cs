using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Procurement.Application.Commands.CancelBatch;

internal sealed class CancelBatchCommandHandler(
    IProcurementBatchRepository batches,
    IConfirmedOrderReader orders,
    TimeProvider timeProvider)
    : IRequestHandler<CancelBatchCommand, Result<ProcurementBatchDto>>
{
    public async Task<Result<ProcurementBatchDto>> Handle(
        CancelBatchCommand request,
        CancellationToken cancellationToken)
    {
        var batch = await batches.FindByIdAsync(request.BatchId, cancellationToken);
        if (batch is null)
        {
            return Result<ProcurementBatchDto>.Failure(
                Error.NotFound("PROCUREMENT_BATCH", request.BatchId));
        }

        var cancellation = batch.Cancel(
            request.Reason,
            timeProvider.GetUtcNow().UtcDateTime);
        if (cancellation.IsFailure)
            return Result<ProcurementBatchDto>.Failure(cancellation.Error);

        await batches.SaveChangesAsync(cancellationToken);

        var orderIds = batch.Orders
            .Select(link => link.OrderId)
            .Distinct()
            .ToArray();
        var statuses = await orders.ReadStatusesAsync(orderIds, cancellationToken);

        return Result<ProcurementBatchDto>.Success(
            ProcurementBatchDtoMapper.Map(batch, statuses));
    }
}

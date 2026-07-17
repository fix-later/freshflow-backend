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
    /// <summary>
    /// Order statuses <c>Order.CancelWithSession</c> still accepts. Declared as strings because
    /// Procurement may not reference the Orders module. An already-cancelled order is a no-op, not
    /// a blocker.
    /// </summary>
    private static readonly string[] CancellableOrderStatuses = ["Confirmed", "Batched", "Cancelled"];

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

        var orderIds = batch.Orders
            .Select(link => link.OrderId)
            .Distinct()
            .ToArray();
        var statuses = await orders.ReadStatusesAsync(orderIds, cancellationToken);

        // Cancelling a session promises to cancel every order it covers. An order advanced past
        // Batched on its own is beyond CancelWithSession's reach, so cancelling the batch anyway
        // would strand it: a cancelled session still carrying a live order.
        var strandedOrderIds = statuses
            .Where(entry => !CancellableOrderStatuses.Contains(entry.Value))
            .Select(entry => entry.Key)
            .ToArray();
        if (strandedOrderIds.Length > 0)
        {
            return Result<ProcurementBatchDto>.Failure(Error.Conflict(
                "BATCH_NOT_CANCELLABLE",
                $"Procurement batch '{batch.Id}' cannot be cancelled because order(s) " +
                $"'{string.Join("', '", strandedOrderIds)}' have already moved past batching."));
        }

        var cancellation = batch.Cancel(
            request.Reason,
            timeProvider.GetUtcNow().UtcDateTime);
        if (cancellation.IsFailure)
            return Result<ProcurementBatchDto>.Failure(cancellation.Error);

        await batches.SaveChangesAsync(cancellationToken);

        // Re-read: saving dispatches the domain event that cancels the covered orders, so the
        // statuses fetched for the gate above are already stale by the time we answer.
        var settledStatuses = await orders.ReadStatusesAsync(orderIds, cancellationToken);

        return Result<ProcurementBatchDto>.Success(
            ProcurementBatchDtoMapper.Map(batch, settledStatuses));
    }
}

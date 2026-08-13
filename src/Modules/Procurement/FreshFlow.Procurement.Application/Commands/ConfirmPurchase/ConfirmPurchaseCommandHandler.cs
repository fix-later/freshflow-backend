using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Procurement.Application.Commands.ConfirmPurchase;

internal sealed class ConfirmPurchaseCommandHandler(
    IProcurementBatchRepository batches,
    IConfirmedOrderReader orders,
    TimeProvider timeProvider)
    : IRequestHandler<ConfirmPurchaseCommand, Result<ProcurementBatchDto>>
{
    public async Task<Result<ProcurementBatchDto>> Handle(
        ConfirmPurchaseCommand request,
        CancellationToken cancellationToken)
    {
        var batch = await batches.FindByIdAsync(request.BatchId, cancellationToken);
        if (batch is null ||
            !batch.Items.Any(item => item.AssignedAgentUserId == request.AgentUserId))
        {
            return Result<ProcurementBatchDto>.Failure(
                Error.NotFound("PROCUREMENT_BATCH", request.BatchId));
        }

        var lines = new Dictionary<Guid, (int ActualQuantity, decimal ActualUnitPrice)>();
        foreach (var line in request.Lines)
        {
            if (!lines.TryAdd(
                    line.MarketProductId,
                    (line.ActualQuantity, line.ActualUnitPrice)))
            {
                return Result<ProcurementBatchDto>.Failure(Error.Validation(
                    "PURCHASE_LINES_MISMATCH",
                    "Purchase confirmation must contain exactly one line for every batch item."));
            }
        }

        var confirmation = batch.ConfirmPurchase(
            request.AgentUserId,
            lines,
            timeProvider.GetUtcNow().UtcDateTime);
        if (confirmation.IsFailure)
            return Result<ProcurementBatchDto>.Failure(confirmation.Error);

        if (!await batches.SaveChangesAsync(cancellationToken))
        {
            return Result<ProcurementBatchDto>.Failure(Error.Conflict(
                "OPTIMISTIC_CONCURRENCY_CONFLICT",
                "The procurement batch changed concurrently. Retry the request."));
        }

        var orderIds = batch.Orders
            .Select(link => link.OrderId)
            .Distinct()
            .ToArray();
        var statuses = await orders.ReadStatusesAsync(orderIds, cancellationToken);

        return Result<ProcurementBatchDto>.Success(
            ProcurementBatchDtoMapper.Map(batch, statuses));
    }
}

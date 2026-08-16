using System.Text.Json;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Procurement.Application.Commands.ResetBatchingDay;

internal sealed class ResetBatchingDayCommandHandler(
    IProcurementBatchRepository batches,
    IAuditLogWriter auditLogs,
    TimeProvider timeProvider)
    : IRequestHandler<ResetBatchingDayCommand, Result<BatchingResetResult>>
{
    public async Task<Result<BatchingResetResult>> Handle(
        ResetBatchingDayCommand request,
        CancellationToken cancellationToken)
    {
        var resetAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        var reset = await batches.ResetDayAsync(
            request.TargetDate,
            resetAtUtc,
            cancellationToken);
        if (reset.IsFailure)
            return Result<BatchingResetResult>.Failure(reset.Error);

        var operationId = Guid.NewGuid();
        await auditLogs.WriteAsync(
            request.ActorId,
            "procurement_batching_reset",
            "procurement_batch_cycle",
            operationId,
            JsonSerializer.Serialize(new
            {
                targetDate = request.TargetDate,
                batchesReset = reset.Value.BatchesReset,
                ordersReset = reset.Value.OrdersReset
            }),
            resetAtUtc,
            cancellationToken);

        return Result<BatchingResetResult>.Success(new BatchingResetResult(
            operationId,
            request.TargetDate,
            reset.Value.BatchesReset,
            reset.Value.OrdersReset));
    }
}

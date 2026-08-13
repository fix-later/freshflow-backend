using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.Procurement.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Procurement.Application.Commands.ReportException;

internal sealed class ReportExceptionCommandHandler(
    IProcurementBatchRepository batches,
    IConfirmedOrderReader orders,
    TimeProvider timeProvider)
    : IRequestHandler<ReportExceptionCommand, Result<ProcurementBatchDto>>
{
    public async Task<Result<ProcurementBatchDto>> Handle(
        ReportExceptionCommand request,
        CancellationToken cancellationToken)
    {
        var batch = await batches.FindByIdAsync(request.BatchId, cancellationToken);
        if (batch is null ||
            !batch.Items.Any(item => item.AssignedAgentUserId == request.AgentUserId))
        {
            return Result<ProcurementBatchDto>.Failure(
                Error.NotFound("PROCUREMENT_BATCH", request.BatchId));
        }

        if (!Enum.TryParse<ProcurementExceptionType>(request.Type, true, out var type) ||
            !Enum.IsDefined(type))
        {
            return Result<ProcurementBatchDto>.Failure(Error.Validation(
                "INVALID_EXCEPTION_TYPE",
                "Procurement exception type is invalid."));
        }

        var report = batch.ReportException(
            request.MarketProductId,
            type,
            request.ReportedQuantity,
            request.Note,
            request.ProofImageUrl,
            request.AgentUserId,
            timeProvider.GetUtcNow().UtcDateTime);
        if (report.IsFailure)
            return Result<ProcurementBatchDto>.Failure(report.Error);

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

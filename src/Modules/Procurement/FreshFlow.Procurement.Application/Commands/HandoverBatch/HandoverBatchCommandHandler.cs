using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Procurement.Application.Commands.HandoverBatch;

internal sealed class HandoverBatchCommandHandler(
    IProcurementBatchRepository batches,
    IConfirmedOrderReader orders,
    IHubByMarketReader hubs,
    TimeProvider timeProvider)
    : IRequestHandler<HandoverBatchCommand, Result<ProcurementBatchDto>>
{
    public async Task<Result<ProcurementBatchDto>> Handle(
        HandoverBatchCommand request,
        CancellationToken cancellationToken)
    {
        var batch = await batches.FindByIdAsync(request.BatchId, cancellationToken);
        if (batch is null || batch.AssignedAgentUserId != request.AgentUserId)
        {
            return Result<ProcurementBatchDto>.Failure(
                Error.NotFound("PROCUREMENT_BATCH", request.BatchId));
        }

        if (batch.HubId is null)
        {
            return Result<ProcurementBatchDto>.Failure(Error.Validation(
                "HUB_NOT_CONFIGURED_FOR_MARKET",
                $"Procurement batch '{batch.Id}' has no resolved hub."));
        }

        if (!await hubs.IsActiveAsync(batch.HubId.Value, cancellationToken))
        {
            return Result<ProcurementBatchDto>.Failure(Error.Validation(
                "HUB_INACTIVE",
                $"Hub '{batch.HubId}' is inactive."));
        }

        var handover = batch.HandoverToHub(timeProvider.GetUtcNow().UtcDateTime);
        if (handover.IsFailure)
            return Result<ProcurementBatchDto>.Failure(handover.Error);

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

using FreshFlow.Contracts;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.Procurement.Application.EventHandlers;
using FreshFlow.Procurement.Domain.Events;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Procurement.Application.Commands.HandoverBatch;

internal sealed class HandoverBatchCommandHandler(
    IProcurementBatchRepository batches,
    IConfirmedOrderReader orders,
    IHubByMarketReader hubs,
    IProcurementHandoverOrderFinalizer orderFinalizer,
    IPublisher publisher,
    TimeProvider timeProvider,
    ILogger<HandoverBatchCommandHandler>? logger = null)
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

        ProcurementBatchDto? response = null;
        ProcurementBatchHandedOffIntegrationEvent? integrationEvent = null;
        IReadOnlyList<INotification> orderEvents = [];
        var transaction = await batches.ExecuteInSerializableTransactionAsync(async ct =>
        {
            var domainEvent = batch.DomainEvents
                .OfType<ProcurementBatchHandedOffDomainEvent>()
                .Single();
            integrationEvent = ProcurementBatchHandedOffDomainEventHandler.Map(domainEvent);
            batch.ClearDomainEvents();

            try
            {
                orderEvents = await orderFinalizer.FinalizeAsync(integrationEvent, ct);
            }
            catch (ProcurementHandoverRejectedException ex)
            {
                return Result.Failure(Error.Conflict(ex.Code, ex.Message));
            }

            if (!await batches.SaveChangesAsync(ct))
            {
                return Result.Failure(Error.Conflict(
                    "PROCUREMENT_HANDOVER_CONFLICT",
                    "The procurement batch changed concurrently. Retry the handover."));
            }

            var orderIds = batch.Orders
                .Select(link => link.OrderId)
                .Distinct()
                .ToArray();
            var statuses = await orders.ReadStatusesAsync(orderIds, ct);
            response = ProcurementBatchDtoMapper.Map(batch, statuses);

            return Result.Success();
        }, cancellationToken);

        if (transaction.IsFailure)
            return Result<ProcurementBatchDto>.Failure(transaction.Error);

        foreach (var notification in orderEvents.Append<INotification>(integrationEvent!))
        {
            try
            {
                await publisher.Publish(notification, cancellationToken);
            }
            catch (OperationCanceledException ex)
            {
                logger?.LogWarning(
                    ex,
                    "Post-commit procurement handover dispatch cancelled for BatchId={BatchId}.",
                    batch.Id);
                break;
            }
            catch (Exception ex)
            {
                logger?.LogError(
                    ex,
                    "Post-commit procurement handover dispatch failed for BatchId={BatchId}.",
                    batch.Id);
            }
        }

        return Result<ProcurementBatchDto>.Success(response!);
    }
}

using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Procurement.Application.Commands.AssignBatchItems;

internal sealed class AssignBatchItemsCommandHandler(
    IProcurementBatchRepository batches,
    IMarketAgentReader marketAgents,
    IConfirmedOrderReader orders,
    TimeProvider timeProvider)
    : IRequestHandler<AssignBatchItemsCommand, Result<ProcurementBatchDto>>
{
    public async Task<Result<ProcurementBatchDto>> Handle(
        AssignBatchItemsCommand request,
        CancellationToken cancellationToken)
    {
        var batch = await batches.FindByIdAsync(request.BatchId, cancellationToken);
        if (batch is null)
        {
            return Result<ProcurementBatchDto>.Failure(
                Error.NotFound("PROCUREMENT_BATCH", request.BatchId));
        }

        foreach (var agentUserId in request.Assignments
                     .Where(assignment => assignment.AgentUserId != Guid.Empty)
                     .Select(assignment => assignment.AgentUserId)
                     .Distinct())
        {
            if (!await marketAgents.IsEligibleMarketAgentAsync(
                    agentUserId,
                    batch.MarketId,
                    cancellationToken))
            {
                return Result<ProcurementBatchDto>.Failure(Error.Validation(
                    "AGENT_NOT_ELIGIBLE",
                    "User is not an active market agent assigned to this market."));
            }
        }

        var assignments = new Dictionary<Guid, Guid>();
        if (request.Assignments.Any(assignment =>
                !assignments.TryAdd(assignment.MarketProductId, assignment.AgentUserId)))
        {
            return Result<ProcurementBatchDto>.Failure(Error.Validation(
                "VALIDATION_ERROR",
                "Item assignments must contain unique market product IDs."));
        }

        var assignment = batch.AssignItems(
            assignments,
            timeProvider.GetUtcNow().UtcDateTime);
        if (assignment.IsFailure)
            return Result<ProcurementBatchDto>.Failure(assignment.Error);

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

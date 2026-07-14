using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Procurement.Application.Commands.AssignAgent;

internal sealed class AssignAgentCommandHandler(
    IProcurementBatchRepository batches,
    IMarketAgentReader marketAgents,
    IConfirmedOrderReader orders,
    TimeProvider timeProvider)
    : IRequestHandler<AssignAgentCommand, Result<ProcurementBatchDto>>
{
    public async Task<Result<ProcurementBatchDto>> Handle(
        AssignAgentCommand request,
        CancellationToken cancellationToken)
    {
        var batch = await batches.FindByIdAsync(request.BatchId, cancellationToken);
        if (batch is null)
        {
            return Result<ProcurementBatchDto>.Failure(
                Error.NotFound("PROCUREMENT_BATCH", request.BatchId));
        }

        var isEligible = await marketAgents.IsEligibleMarketAgentAsync(
            request.AgentUserId,
            batch.MarketId,
            cancellationToken);
        if (!isEligible)
        {
            return Result<ProcurementBatchDto>.Failure(Error.Validation(
                "AGENT_NOT_ELIGIBLE",
                "User is not an active market agent assigned to this market."));
        }

        var assignment = batch.AssignAgent(
            request.AgentUserId,
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

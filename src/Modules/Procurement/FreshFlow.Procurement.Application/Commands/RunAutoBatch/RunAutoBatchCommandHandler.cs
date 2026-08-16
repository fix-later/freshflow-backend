using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.Procurement.Application.Services;
using FreshFlow.Procurement.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Procurement.Application.Commands.RunAutoBatch;

internal sealed class RunAutoBatchCommandHandler(
    IProcurementBatchingService batchingService,
    IOperationalSettingsReader settings,
    TimeProvider timeProvider,
    IMarketSessionRepository sessions)
    : IRequestHandler<RunAutoBatchCommand, Result<BatchingResult>>
{
    public async Task<Result<BatchingResult>> Handle(
        RunAutoBatchCommand request,
        CancellationToken cancellationToken)
    {
        var batchDate = request.TargetDate;
        if (!batchDate.HasValue)
        {
            var operationalSettings = await settings.ReadAsync(cancellationToken);
            batchDate = ProcurementBatchCycle.ResolveDueBatchDate(
                timeProvider.GetUtcNow(),
                operationalSettings.DailyCutoffTime);
        }

        var closed = await sessions.ListAsync(
            batchDate, batchDate, null, MarketSessionStatus.Closed, cancellationToken);
        if (closed.Count == 0)
            return Result<BatchingResult>.Success(new BatchingResult(
                0, 0, 0, true, "no_closed_sessions", []));

        var results = new List<BatchingResult>(closed.Count);
        foreach (var session in closed)
        {
            var result = await batchingService.BuildSessionBatchAsync(
                session.Id, request.DryRun, cancellationToken);
            if (result.IsFailure)
                return result;
            results.Add(result.Value);
        }

        return Result<BatchingResult>.Success(new BatchingResult(
            results.Sum(result => result.BatchesCreated),
            results.Sum(result => result.OrdersBatched),
            results.Sum(result => result.ItemsAggregated),
            results.All(result => result.Skipped),
            results.All(result => result.Skipped) ? "all_sessions_skipped" : null,
            results.SelectMany(result => result.Preview).ToList()));
    }
}

using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.Procurement.Application.Services;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Procurement.Application.Commands.RunAutoBatch;

internal sealed class RunAutoBatchCommandHandler(
    IProcurementBatchingService batchingService,
    IOperationalSettingsReader settings,
    TimeProvider timeProvider)
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

        return await batchingService.BuildBatchesAsync(
            batchDate.Value,
            request.DryRun,
            request.Force,
            cancellationToken);
    }
}

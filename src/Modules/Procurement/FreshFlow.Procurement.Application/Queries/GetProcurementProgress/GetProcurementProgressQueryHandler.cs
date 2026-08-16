using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Procurement.Application.Queries.GetProcurementProgress;

internal sealed class GetProcurementProgressQueryHandler(
    IProcurementBatchRepository batches)
    : IRequestHandler<GetProcurementProgressQuery, Result<ProcurementProgressDto>>
{
    public async Task<Result<ProcurementProgressDto>> Handle(
        GetProcurementProgressQuery request,
        CancellationToken cancellationToken)
    {
        ProcurementBatchStatus? status = null;
        if (request.Status is not null)
        {
            if (!Enum.TryParse<ProcurementBatchStatus>(request.Status, true, out var parsed) ||
                !Enum.IsDefined(parsed))
            {
                return Result<ProcurementProgressDto>.Failure(Error.Validation(
                    "INVALID_BATCH_STATUS",
                    $"Procurement batch status '{request.Status}' is invalid."));
            }

            status = parsed;
        }

        var batchDate = request.Date ??
            await batches.GetLatestCycleDateAsync(cancellationToken);
        if (batchDate is null)
            return Result<ProcurementProgressDto>.Success(Map(null, [], status));

        var cycleBatches = await batches.ListByDateAsync(
            batchDate.Value,
            cancellationToken);

        return Result<ProcurementProgressDto>.Success(
            Map(batchDate, cycleBatches, status));
    }

    private static ProcurementProgressDto Map(
        DateOnly? batchDate,
        IReadOnlyList<ProcurementBatch> batches,
        ProcurementBatchStatus? status)
    {
        var rows = batches.Select(MapBatch).ToList();
        var statusCounts = Enum.GetValues<ProcurementBatchStatus>()
            .ToDictionary(
                value => value.ToString(),
                value => rows.Count(row => row.Status == value.ToString()));
        var summary = new ProcurementProgressSummaryDto(
            batchDate,
            rows.Count,
            statusCounts,
            rows.Sum(row => row.ItemsTotal),
            rows.Sum(row => row.ItemsPurchased),
            rows.Sum(row => row.ItemsPending),
            rows.Sum(row => row.ExceptionCount));
        var filteredRows = status is null
            ? rows
            : rows.Where(row => row.Status == status.Value.ToString()).ToList();

        return new ProcurementProgressDto(
            summary,
            filteredRows.AsReadOnly());
    }

    private static ProcurementBatchProgressDto MapBatch(ProcurementBatch batch)
    {
        var itemsPurchased = batch.Items.Count(item => item.ActualQuantity is not null);
        var exceptionCount = batch.Exceptions.Count(exception => !exception.IsDeleted);

        // A cancelled session's unbought items are not work anyone still owes, so they must stay out
        // of the pending totals this endpoint exists to report.
        var itemsPending = batch.Status == ProcurementBatchStatus.Cancelled
            ? 0
            : batch.Items.Count - itemsPurchased;

        return new ProcurementBatchProgressDto(
            batch.Id,
            batch.MarketId,
            batch.Status.ToString(),
            batch.AssignedAgentUserId,
            batch.HubId,
            batch.Items.Count,
            itemsPurchased,
            itemsPending,
            exceptionCount,
            batch.Orders.Count,
            batch.ManifestedAt,
            batch.AssignedAt,
            batch.HandedOffAt);
    }
}

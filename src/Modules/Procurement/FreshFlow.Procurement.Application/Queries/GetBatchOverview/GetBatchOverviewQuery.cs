using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Procurement.Application.Queries.GetBatchOverview;

public sealed record GetBatchOverviewQuery(Guid BatchId)
    : IQuery<ProcurementBatchOverviewDto>;

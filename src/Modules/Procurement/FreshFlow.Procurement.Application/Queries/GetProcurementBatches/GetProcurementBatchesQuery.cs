using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Procurement.Application.Queries.GetProcurementBatches;

public sealed record GetProcurementBatchesQuery(
    int Page = 1,
    int PageSize = 20) : IQuery<ProcurementBatchListDto>;

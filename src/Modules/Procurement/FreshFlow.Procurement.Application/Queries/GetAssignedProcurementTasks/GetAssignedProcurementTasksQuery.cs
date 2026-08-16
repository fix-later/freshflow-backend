using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Procurement.Application.Queries.GetAssignedProcurementTasks;

public sealed record GetAssignedProcurementTasksQuery(
    Guid AgentUserId,
    int Page = 1,
    int PageSize = 20) : IQuery<ProcurementBatchListDto>;

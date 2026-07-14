using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Procurement.Application.Queries.GetAssignedProcurementTask;

public sealed record GetAssignedProcurementTaskQuery(
    Guid AgentUserId,
    Guid BatchId) : IQuery<ProcurementBatchDto>;

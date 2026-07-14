using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Procurement.Application.Commands.HandoverBatch;

public sealed record HandoverBatchCommand(
    Guid BatchId,
    Guid AgentUserId,
    Guid? HubId) : ICommand<ProcurementBatchDto>;

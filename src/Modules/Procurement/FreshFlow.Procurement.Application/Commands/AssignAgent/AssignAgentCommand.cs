using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Procurement.Application.Commands.AssignAgent;

public sealed record AssignAgentCommand(
    Guid BatchId,
    Guid AgentUserId) : ICommand<ProcurementBatchDto>;

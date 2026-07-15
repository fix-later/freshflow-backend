using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Procurement.Application.Commands.ReportException;

public sealed record ReportExceptionCommand(
    Guid BatchId,
    Guid AgentUserId,
    Guid MarketProductId,
    string Type,
    int ReportedQuantity,
    string? Note,
    string? ProofImageUrl) : ICommand<ProcurementBatchDto>;

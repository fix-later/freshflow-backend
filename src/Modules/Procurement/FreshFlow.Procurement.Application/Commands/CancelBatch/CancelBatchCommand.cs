using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Procurement.Application.Commands.CancelBatch;

public sealed record CancelBatchCommand(
    Guid BatchId,
    string? Reason) : ICommand<ProcurementBatchDto>;

using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Procurement.Application.Commands.AssignBatchItems;

public sealed record AssignBatchItemsCommand(
    Guid BatchId,
    IReadOnlyList<ItemAssignmentDto> Assignments) : ICommand<ProcurementBatchDto>;

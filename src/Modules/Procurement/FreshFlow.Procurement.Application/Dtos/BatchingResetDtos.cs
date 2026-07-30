namespace FreshFlow.Procurement.Application.Dtos;

public sealed record BatchingResetCounts(
    int BatchesReset,
    int OrdersReset);

public sealed record BatchingResetResult(
    Guid OperationId,
    DateOnly TargetDate,
    int BatchesReset,
    int OrdersReset);

using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Procurement.Application.Commands.ConfirmPurchase;

public sealed record PurchaseLineDto(
    Guid MarketProductId,
    int ActualQuantity,
    decimal ActualUnitPrice);

public sealed record ConfirmPurchaseCommand(
    Guid BatchId,
    Guid AgentUserId,
    IReadOnlyList<PurchaseLineDto> Lines) : ICommand<ProcurementBatchDto>;

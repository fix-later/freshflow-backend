using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Commands.ConfirmOrderReceipt;

/// <summary>
/// SCRUM-222 — UC-ORD-18: restaurant confirms receipt of a delivered order.
/// </summary>
public sealed record ConfirmOrderReceiptCommand(
    Guid UserId,
    Guid OrderId) : ICommand<OrderDto>;

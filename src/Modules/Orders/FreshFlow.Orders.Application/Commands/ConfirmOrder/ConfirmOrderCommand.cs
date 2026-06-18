using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Commands.ConfirmOrder;

/// <summary>
/// UC-ORD-06/07/08 — confirms a draft order: checks B2B credit, locks item prices,
/// applies the 22:00 cutoff (pushing to the next delivery cycle if needed), and charges
/// the restaurant's credit account.
/// </summary>
public sealed record ConfirmOrderCommand(Guid UserId, Guid OrderId) : ICommand<OrderDto>;

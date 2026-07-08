using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Commands.SettleRestaurantCredit;

public sealed record SettleRestaurantCreditCommand(
    Guid RestaurantId,
    decimal Amount,
    PaymentMethod PaymentMethod,
    string? Reference,
    string? Note) : ICommand<RestaurantCreditDto>;

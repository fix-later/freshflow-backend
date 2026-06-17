using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Commands.SettleRestaurantCredit;

public sealed record SettleRestaurantCreditCommand(
    Guid RestaurantId,
    decimal Amount,
    string? Note) : ICommand<RestaurantCreditDto>;

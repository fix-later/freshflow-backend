using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Commands.SetRestaurantCreditLimit;

public sealed record SetRestaurantCreditLimitCommand(
    Guid RestaurantId,
    decimal CreditLimit,
    string? Note) : ICommand<RestaurantCreditDto>;

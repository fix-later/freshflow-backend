using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Queries.GetRestaurantCredit;

public sealed record GetRestaurantCreditQuery(
    Guid UserId,
    bool IsAdmin,
    Guid RestaurantId) : IQuery<RestaurantCreditDto>;

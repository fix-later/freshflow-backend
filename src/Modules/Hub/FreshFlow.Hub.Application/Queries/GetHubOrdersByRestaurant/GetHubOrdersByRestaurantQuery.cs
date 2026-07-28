using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Queries.GetHubOrdersByRestaurant;

public sealed record GetHubOrdersByRestaurantQuery(
    Guid HubId,
    DateOnly ServiceDate,
    bool IncludeBatched = false)
    : IQuery<HubOrdersByRestaurantDto>;

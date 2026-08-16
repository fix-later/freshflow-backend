using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Queries.GetHubOrdersByRestaurant;

public sealed record GetHubOrdersByRestaurantQuery(
    Guid HubId,
    DateOnly ServiceDate,
    bool IncludeBatched = false,
    Guid ActorUserId = default,
    bool BypassHubAssignment = false)
    : IQuery<HubOrdersByRestaurantDto>, IHubAccessRequest;

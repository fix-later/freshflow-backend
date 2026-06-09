using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Auth.Application.Queries.GetRestaurantProfile;

public sealed record GetRestaurantProfileQuery(Guid UserId)
    : IQuery<GetRestaurantProfileResponse>;

using FreshFlow.Auth.Application.Queries.GetRestaurantProfile;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Auth.Application.Queries.GetRestaurantProfileById;

/// <summary>Admin lookup of a restaurant profile by its restaurant id (not the owning user id).</summary>
public sealed record GetRestaurantProfileByIdQuery(Guid RestaurantId)
    : IQuery<GetRestaurantProfileResponse>;

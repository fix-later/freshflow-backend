using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Queries.GetUsers;

internal sealed class GetUsersQueryHandler(
    IUserRepository users,
    IRestaurantRepository restaurants) : IRequestHandler<GetUsersQuery, Result<GetUsersResponse>>
{
    public async Task<Result<GetUsersResponse>> Handle(GetUsersQuery request, CancellationToken ct)
    {
        var (data, total) = await users.GetPagedAsync(
            request.Role, request.IsActive, request.Search,
            request.Page, request.PageSize, request.RestaurantStatus, ct);

        var dtos = new List<UserSummaryDto>(data.Count);
        foreach (var user in data)
        {
            bool? isApproved = null;
            Guid? restaurantId = null;
            string? restaurantStatus = null;
            if (user.Role.Name == RoleNames.Restaurant)
            {
                var restaurant = await restaurants.FindByUserIdAsync(user.Id, ct);
                isApproved = restaurant?.IsApproved;
                restaurantId = restaurant?.Id;
                restaurantStatus = restaurant?.Status.ToString().ToLowerInvariant();
            }

            dtos.Add(new UserSummaryDto(
                user.Id, user.Email, user.Role.Name,
                user.IsActive, isApproved, restaurantId, restaurantStatus, user.CreatedAt));
        }

        return Result<GetUsersResponse>.Success(new GetUsersResponse(
            dtos,
            new PaginationMeta(request.Page, request.PageSize, total)));
    }
}

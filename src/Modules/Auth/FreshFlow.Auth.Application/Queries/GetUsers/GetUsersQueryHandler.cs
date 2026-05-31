using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Domain.Aggregates;
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
            request.Page, request.PageSize, ct);

        var dtos = new List<UserSummaryDto>(data.Count);
        foreach (var user in data)
        {
            bool? isApproved = null;
            if (user.Role == UserRole.Restaurant)
            {
                var restaurant = await restaurants.FindByUserIdAsync(user.Id, ct);
                isApproved = restaurant?.IsApproved;
            }

            dtos.Add(new UserSummaryDto(
                user.Id, user.Email, user.Role.ToApiString(),
                user.IsActive, isApproved, user.CreatedAt));
        }

        return Result<GetUsersResponse>.Success(new GetUsersResponse(
            dtos,
            new PaginationMeta(request.Page, request.PageSize, total)));
    }
}

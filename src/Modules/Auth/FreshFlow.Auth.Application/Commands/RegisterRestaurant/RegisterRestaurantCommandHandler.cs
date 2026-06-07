using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Domain.Aggregates;
using FreshFlow.Auth.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Commands.RegisterRestaurant;

internal sealed class RegisterRestaurantCommandHandler(
    IUserRepository users,
    IRoleRepository roles,
    IPasswordHasher hasher,
    IRestaurantRepository restaurants) : IRequestHandler<RegisterRestaurantCommand, Result<RegisterRestaurantResponse>>
{
    public async Task<Result<RegisterRestaurantResponse>> Handle(
        RegisterRestaurantCommand request, CancellationToken ct)
    {
        if (await users.ExistsAsync(request.Email, ct))
            return Result<RegisterRestaurantResponse>.Failure(
                Error.Conflict("EMAIL_ALREADY_EXISTS", $"A user with email '{request.Email}' already exists."));

        var role = await roles.FindByNameAsync(RoleNames.Restaurant, ct);
        if (role is null)
            return Result<RegisterRestaurantResponse>.Failure(
                Error.Validation("VALIDATION_ERROR", "Role 'restaurant' is not configured."));

        var passwordHash = hasher.Hash(request.Password);
        var user = User.Create(request.Email, passwordHash, role, request.Phone);

        await users.AddAsync(user, ct);
        // No SaveChangesAsync here — CreateAsync commits both User and RestaurantRow
        // atomically via the shared scoped AppDbContext.
        var restaurantId = await restaurants.CreateAsync(user.Id, request.RestaurantName, ct);

        return Result<RegisterRestaurantResponse>.Success(new RegisterRestaurantResponse(
            user.Id,
            restaurantId,
            user.Email,
            request.RestaurantName,
            IsApproved: false));
    }
}

using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Domain.Aggregates;
using FreshFlow.Auth.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Commands.Admin.CreateUser;

internal sealed class CreateUserCommandHandler(
    IUserRepository users,
    IRoleRepository roles,
    IPasswordHasher hasher,
    IRestaurantRepository restaurants,
    IDriverProfileCreator driverProfileCreator,
    IMarketValidator marketValidator,
    IUserMarketAssignmentRepository marketAssignments) : IRequestHandler<CreateUserCommand, Result<CreateUserResponse>>
{
    public async Task<Result<CreateUserResponse>> Handle(CreateUserCommand request, CancellationToken ct)
    {
        if (await users.ExistsAsync(request.Email, ct))
            return Result<CreateUserResponse>.Failure(
                Error.Conflict("EMAIL_ALREADY_EXISTS", $"A user with email '{request.Email}' already exists."));

        if (!string.IsNullOrWhiteSpace(request.Phone) &&
            await users.ExistsByPhoneAsync(request.Phone, ct))
            return Result<CreateUserResponse>.Failure(
                Error.Conflict("PHONE_ALREADY_EXISTS", $"A user with phone '{request.Phone}' already exists."));

        // Normalise the kiosk_staff alias used in older API versions
        var roleName = request.Role.ToLowerInvariant() == "kiosk_staff"
            ? RoleNames.MarketAgent
            : request.Role.ToLowerInvariant();

        var role = await roles.FindByNameAsync(roleName, ct);
        if (role is null)
            return Result<CreateUserResponse>.Failure(
                Error.Validation("VALIDATION_ERROR", $"Invalid role: {request.Role}"));

        // Validate market exists for market_agent
        if (roleName == RoleNames.MarketAgent && request.MarketId.HasValue)
        {
            var isValid = await marketValidator.IsActiveMarketAsync(request.MarketId.Value, ct);
            if (!isValid)
                return Result<CreateUserResponse>.Failure(
                    Error.Validation("INVALID_MARKET", $"Market '{request.MarketId}' does not exist or is inactive."));
        }

        var passwordHash = hasher.Hash(request.Password);
        var user = User.Create(request.Email, passwordHash, role, request.Phone);
        await users.AddAsync(user, ct);

        if (roleName == RoleNames.MarketAgent && request.MarketId.HasValue)
            await marketAssignments.AddAsync(
                new Domain.Entities.UserMarketAssignment(user.Id, request.MarketId.Value, assignedBy: null), ct);

        await users.SaveChangesAsync(ct);

        if (roleName == RoleNames.Restaurant)
            await restaurants.CreateAsync(user.Id, request.RestaurantName!, ct);
        else if (roleName == RoleNames.Driver)
            await driverProfileCreator.CreateAsync(user.Id, ct);

        return Result<CreateUserResponse>.Success(new CreateUserResponse(
            user.Id, user.Email, user.Role.Name, user.IsActive, user.CreatedAt));
    }
}

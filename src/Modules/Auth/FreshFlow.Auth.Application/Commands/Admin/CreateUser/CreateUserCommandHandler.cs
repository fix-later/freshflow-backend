using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Domain.Aggregates;
using FreshFlow.Auth.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Commands.Admin.CreateUser;

internal sealed class CreateUserCommandHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    IRestaurantRepository restaurants,
    IDriverProfileCreator driverProfileCreator,
    IMarketValidator marketValidator) : IRequestHandler<CreateUserCommand, Result<CreateUserResponse>>
{
    public async Task<Result<CreateUserResponse>> Handle(CreateUserCommand request, CancellationToken ct)
    {
        if (await users.ExistsAsync(request.Email, ct))
            return Result<CreateUserResponse>.Failure(
                Error.Conflict("EMAIL_ALREADY_EXISTS", $"A user with email '{request.Email}' already exists."));

        UserRole role;
        try { role = UserRoleExtensions.FromApiString(request.Role); }
        catch (ArgumentException)
        {
            return Result<CreateUserResponse>.Failure(
                Error.Validation("VALIDATION_ERROR", $"Invalid role: {request.Role}"));
        }

        // Validate market exists for market_agent
        if (role == UserRole.MarketAgent && request.MarketId.HasValue)
        {
            var isValid = await marketValidator.IsActiveMarketAsync(request.MarketId.Value, ct);
            if (!isValid)
                return Result<CreateUserResponse>.Failure(
                    Error.Validation("INVALID_MARKET", $"Market '{request.MarketId}' does not exist or is inactive."));
        }

        var passwordHash = hasher.Hash(request.Password);
        var user = User.Create(request.Email, passwordHash, role);
        await users.AddAsync(user, ct);
        await users.SaveChangesAsync(ct);

        if (role == UserRole.Restaurant)
            await restaurants.CreateAsync(user.Id, request.RestaurantName!, ct);
        else if (role == UserRole.Driver)
            await driverProfileCreator.CreateAsync(user.Id, ct);

        return Result<CreateUserResponse>.Success(new CreateUserResponse(
            user.Id, user.Email, user.Role.ToApiString(), user.IsActive, user.CreatedAt));
    }
}

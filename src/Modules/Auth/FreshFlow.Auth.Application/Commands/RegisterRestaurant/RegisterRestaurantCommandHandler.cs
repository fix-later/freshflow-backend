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

        // Normalise phone at the boundary so the uniqueness check uses the same
        // trimmed form that User.Create will store (phone?.Trim().ToLowerInvariant()).
        var phone = request.Phone?.Trim();

        if (!string.IsNullOrWhiteSpace(phone) &&
            await users.ExistsByPhoneAsync(phone, ct))
            return Result<RegisterRestaurantResponse>.Failure(
                Error.Conflict("PHONE_ALREADY_EXISTS", $"A user with phone '{phone}' already exists."));

        var role = await roles.FindByNameAsync(RoleNames.Restaurant, ct);
        if (role is null)
            return Result<RegisterRestaurantResponse>.Failure(
                Error.Validation("ROLE_NOT_CONFIGURED", "Role 'restaurant' is not configured in the system."));

        var passwordHash = hasher.Hash(request.Password);
        var user = User.Create(request.Email, passwordHash, role, phone);

        await users.AddAsync(user, ct);
        // No SaveChangesAsync here — CreateAsync commits both User and RestaurantRow
        // atomically via the shared scoped AppDbContext.
        var taxCode = string.IsNullOrWhiteSpace(request.TaxCode) ? null : request.TaxCode.Trim();
        var invoiceLegalName = request.InvoiceLegalName?.Trim();
        var invoiceAddress = request.InvoiceAddress?.Trim();
        var restaurantId = await restaurants.CreateAsync(
            user.Id, request.RestaurantName, taxCode, invoiceLegalName, invoiceAddress, ct);

        return Result<RegisterRestaurantResponse>.Success(new RegisterRestaurantResponse(
            user.Id,
            restaurantId,
            user.Email,
            request.RestaurantName,
            IsApproved: false));
    }
}

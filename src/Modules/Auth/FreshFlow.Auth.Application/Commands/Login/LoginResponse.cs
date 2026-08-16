using FreshFlow.Auth.Domain.Enums;

namespace FreshFlow.Auth.Application.Commands.Login;

public sealed record LoginResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    LoginUserDto User,
    RestaurantStatus? ApprovalStatus = null);

public sealed record LoginUserDto(Guid Id, string Email, string? FullName, string Role);

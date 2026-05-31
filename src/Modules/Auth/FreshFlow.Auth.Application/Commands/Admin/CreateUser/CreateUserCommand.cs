using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Auth.Application.Commands.Admin.CreateUser;

public sealed record CreateUserCommand(
    string Email,
    string Password,
    string Role,
    Guid? MarketId,
    string? RestaurantName) : ICommand<CreateUserResponse>;

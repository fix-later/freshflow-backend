using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Auth.Application.Commands.RegisterRestaurant;

public sealed record RegisterRestaurantCommand(
    string Email,
    string Password,
    string RestaurantName,
    string? Phone,
    string? TaxCode = null) : ICommand<RegisterRestaurantResponse>;

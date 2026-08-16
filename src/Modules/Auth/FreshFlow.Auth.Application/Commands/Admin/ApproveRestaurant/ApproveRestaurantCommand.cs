using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Auth.Application.Commands.Admin.ApproveRestaurant;

public sealed record ApproveRestaurantCommand(Guid RestaurantId) : ICommand<ApproveRestaurantResponse>;

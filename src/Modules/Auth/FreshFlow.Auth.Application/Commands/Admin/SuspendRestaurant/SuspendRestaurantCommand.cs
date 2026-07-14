using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Auth.Application.Commands.Admin.SuspendRestaurant;

public sealed record SuspendRestaurantCommand(Guid RestaurantId) : ICommand<SuspendRestaurantResponse>;

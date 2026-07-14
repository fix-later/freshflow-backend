using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Auth.Application.Commands.Admin.ReactivateRestaurant;

/// <summary>
/// Restores a <c>Suspended</c> restaurant to <c>Active</c>. Unlike ApproveRestaurantCommand
/// (which promotes any non-active restaurant, incl. Pending), this rejects anything that is not
/// currently Suspended so a pending-approval account can never be activated via this path.
/// </summary>
public sealed record ReactivateRestaurantCommand(Guid RestaurantId) : ICommand<ReactivateRestaurantResponse>;

using FreshFlow.Orders.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FreshFlow.Orders.Infrastructure.Realtime;

/// <summary>
/// Real-time hub for restaurant order status updates (UC-ORD-14).
///
/// Restaurant users are automatically joined to <c>restaurant:{restaurantId}</c>
/// based on their JWT subject and restaurant profile. Admin and operations manager users
/// are automatically joined to <c>admin:orders</c> for monitoring.
/// </summary>
[Authorize(Roles = "admin,operations_manager,restaurant")]
public sealed class OrderHub(IRestaurantReader restaurantReader) : Hub
{
    private const string AdminRole = "admin";
    private const string OperationsManagerRole = "operations_manager";
    private const string RestaurantRole = "restaurant";
    internal const string AdminOrdersGroup = "admin:orders";

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.IsInRole(AdminRole) == true
            || Context.User?.IsInRole(OperationsManagerRole) == true)
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId, AdminOrdersGroup, Context.ConnectionAborted);
        }

        if (Context.User?.IsInRole(RestaurantRole) == true)
        {
            if (!Guid.TryParse(Context.User?.FindFirst("sub")?.Value, out var userId))
                throw new HubException("Unable to determine caller identity.");

            var restaurant = await restaurantReader.FindByUserIdAsync(userId, Context.ConnectionAborted);
            if (restaurant is null)
                throw new HubException("Unable to determine caller restaurant.");

            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                RestaurantGroupName(restaurant.RestaurantId),
                Context.ConnectionAborted);
        }

        await base.OnConnectedAsync();
    }

    internal static string RestaurantGroupName(Guid restaurantId) => $"restaurant:{restaurantId}";
}

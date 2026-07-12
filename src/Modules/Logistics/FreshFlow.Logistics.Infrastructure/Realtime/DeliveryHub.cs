using FreshFlow.Logistics.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FreshFlow.Logistics.Infrastructure.Realtime;

[Authorize(Roles = "admin,operations_manager,restaurant")]
public sealed class DeliveryHub(IRestaurantOwnerReader restaurantOwnerReader) : Hub
{
    private const string AdminRole = "admin";
    private const string OperationsManagerRole = "operations_manager";
    private const string RestaurantRole = "restaurant";
    internal const string AdminDeliveryGroup = "admin:delivery";

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.IsInRole(AdminRole) == true
            || Context.User?.IsInRole(OperationsManagerRole) == true)
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                AdminDeliveryGroup,
                Context.ConnectionAborted);
        }

        if (Context.User?.IsInRole(RestaurantRole) == true)
        {
            if (!Guid.TryParse(Context.User?.FindFirst("sub")?.Value, out var userId))
                throw new HubException("Unable to determine caller identity.");

            var restaurantId = await restaurantOwnerReader.FindRestaurantIdByUserIdAsync(
                userId,
                Context.ConnectionAborted);
            if (restaurantId is null)
                throw new HubException("Unable to determine caller restaurant.");

            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                RestaurantGroupName(restaurantId.Value),
                Context.ConnectionAborted);
        }

        await base.OnConnectedAsync();
    }

    internal static string RestaurantGroupName(Guid restaurantId) => $"restaurant:{restaurantId}";
}

namespace FreshFlow.Hub.Domain.Entities;

/// <summary>
/// A driver stationed at a hub. Mirrors <see cref="HubStaffAssignment"/>: the roster is
/// the relation, so a driver can be on more than one hub and a hub on more than one driver.
/// </summary>
/// <remarks>
/// This is where a driver *works*, not what they are *doing*: a delivery job is still
/// <c>DeliveryRoute.DriverUserId</c>, assigned per route. Being on a hub's roster is what
/// makes a driver a candidate for that hub's routes and handovers.
/// </remarks>
public sealed class HubDriverAssignment
{
    private HubDriverAssignment() { } // EF Core

    public HubDriverAssignment(Guid hubId, Guid userId)
    {
        if (hubId == Guid.Empty)
            throw new ArgumentException("Hub ID is required.", nameof(hubId));

        if (userId == Guid.Empty)
            throw new ArgumentException("User ID is required.", nameof(userId));

        HubId = hubId;
        UserId = userId;
    }

    public Guid HubId { get; private set; }
    public Guid UserId { get; private set; }
}

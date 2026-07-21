namespace FreshFlow.Hub.Domain.Entities;

public sealed class HubStaffAssignment
{
    private HubStaffAssignment() { } // EF Core

    public HubStaffAssignment(Guid hubId, Guid userId)
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

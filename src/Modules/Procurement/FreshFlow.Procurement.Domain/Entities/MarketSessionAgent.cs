namespace FreshFlow.Procurement.Domain.Entities;

public sealed class MarketSessionAgent
{
    private MarketSessionAgent() { }

    internal MarketSessionAgent(Guid sessionId, Guid userId, Guid? assignedBy, DateTime assignedAt)
    {
        SessionId = sessionId;
        UserId = userId;
        AssignedBy = assignedBy;
        AssignedAt = assignedAt;
    }

    public Guid SessionId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? AssignedBy { get; private set; }
    public DateTime AssignedAt { get; private set; }
}

namespace FreshFlow.Auth.Domain.Entities;

public sealed class UserMarketAssignment
{
    private UserMarketAssignment() { }  // EF Core

    public UserMarketAssignment(Guid userId, Guid marketId, Guid? assignedBy)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        MarketId = marketId;
        AssignedBy = assignedBy;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid MarketId { get; private set; }
    public Guid? AssignedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
}

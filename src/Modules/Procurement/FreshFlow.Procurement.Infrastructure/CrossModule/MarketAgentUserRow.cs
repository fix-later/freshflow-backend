namespace FreshFlow.Procurement.Infrastructure.CrossModule;

internal sealed class MarketAgentUserRow
{
    public Guid Id { get; set; }
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? DeletedAt { get; set; }
}

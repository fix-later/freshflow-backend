namespace FreshFlow.Hub.Infrastructure.CrossModule;

internal sealed class HubStaffUserRow
{
    public Guid UserId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? DeletedAt { get; set; }
}

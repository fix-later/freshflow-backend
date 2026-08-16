namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class DriverRow
{
    public Guid UserId { get; set; }
    public string? FullName { get; set; }
    public string Email { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public Guid? HubId { get; set; }
}

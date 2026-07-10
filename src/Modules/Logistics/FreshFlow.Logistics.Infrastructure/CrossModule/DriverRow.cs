namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class DriverRow
{
    public Guid UserId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

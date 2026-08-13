namespace FreshFlow.Procurement.Infrastructure.CrossModule;

internal sealed class ConfirmedOrderRow
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? MarketId { get; set; }
    public DateTime? ScheduledFor { get; set; }
    public DateTime? DeletedAt { get; set; }
}

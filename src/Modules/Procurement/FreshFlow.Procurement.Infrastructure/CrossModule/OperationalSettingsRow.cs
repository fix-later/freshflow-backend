namespace FreshFlow.Procurement.Infrastructure.CrossModule;

internal sealed class OperationalSettingsRow
{
    public bool BatchingEnabled { get; set; }
    public TimeOnly DailyCutoffTime { get; set; }
    public int DeliveryWindowDays { get; set; }
}

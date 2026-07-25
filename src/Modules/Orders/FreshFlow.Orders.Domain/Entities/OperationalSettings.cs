using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Orders.Domain.Entities;

/// <summary>
/// Singleton admin-editable operational config (SCRUM-355). At most one row exists;
/// repository callers fall back to <see cref="CreateDefault"/> when no row has been
/// persisted yet. <see cref="BatchingEnabled"/> and <see cref="DefaultRouteType"/> are
/// stored for future Logistics/Hub consumption but have no reader yet (YAGNI) — only
/// <see cref="DailyCutoffTime"/> is currently read, by <c>OrderCutoffScheduler</c>.
/// </summary>
public sealed class OperationalSettings : BaseEntity
{
    private OperationalSettings() { } // EF Core

    public OperationalSettings(
        TimeOnly dailyCutoffTime, bool batchingEnabled, string defaultRouteType, int deliveryWindowDays)
    {
        DailyCutoffTime = dailyCutoffTime;
        BatchingEnabled = batchingEnabled;
        DefaultRouteType = defaultRouteType;
        DeliveryWindowDays = deliveryWindowDays;
    }

    public static OperationalSettings CreateDefault() =>
        new(new TimeOnly(22, 0), batchingEnabled: true, defaultRouteType: "hub_relay", deliveryWindowDays: 7);

    public TimeOnly DailyCutoffTime { get; private set; }
    public bool BatchingEnabled { get; private set; }
    public string DefaultRouteType { get; private set; } = "hub_relay";
    public int DeliveryWindowDays { get; private set; }

    public void Update(
        TimeOnly dailyCutoffTime, bool batchingEnabled, string defaultRouteType, int deliveryWindowDays)
    {
        DailyCutoffTime = dailyCutoffTime;
        BatchingEnabled = batchingEnabled;
        DefaultRouteType = defaultRouteType;
        DeliveryWindowDays = deliveryWindowDays;
        Touch();
    }

    private void Touch() => UpdatedAt = DateTime.UtcNow;
}

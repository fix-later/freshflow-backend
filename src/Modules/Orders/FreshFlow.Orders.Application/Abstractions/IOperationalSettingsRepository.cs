using FreshFlow.Orders.Domain.Entities;

namespace FreshFlow.Orders.Application.Abstractions;

public interface IOperationalSettingsRepository
{
    /// <summary>Returns the persisted singleton row, or <see cref="OperationalSettings.CreateDefault"/> if none exists yet.</summary>
    public Task<OperationalSettings> GetAsync(CancellationToken ct);

    /// <summary>Inserts the singleton row if none exists yet, otherwise updates it in place.</summary>
    public Task<OperationalSettings> UpsertAsync(
        TimeOnly dailyCutoffTime, bool batchingEnabled, string defaultRouteType, CancellationToken ct);
}

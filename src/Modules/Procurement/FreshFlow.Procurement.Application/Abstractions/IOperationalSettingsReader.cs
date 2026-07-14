namespace FreshFlow.Procurement.Application.Abstractions;

public interface IOperationalSettingsReader
{
    public Task<ProcurementOperationalSettingsDto> ReadAsync(CancellationToken ct);
}

public sealed record ProcurementOperationalSettingsDto(
    bool BatchingEnabled,
    TimeOnly DailyCutoffTime);

namespace FreshFlow.Orders.Application.Abstractions;

public interface IMarketSessionGate
{
    public Task<MarketSessionGateResult> CheckAsync(
        Guid? marketId, DateOnly serviceDate, bool lockForConfirmation, CancellationToken ct);
}

public sealed record MarketSessionGateResult(bool Exists, bool IsOpen);

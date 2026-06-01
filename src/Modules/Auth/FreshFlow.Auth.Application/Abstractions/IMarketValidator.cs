namespace FreshFlow.Auth.Application.Abstractions;

public interface IMarketValidator
{
    public Task<bool> IsActiveMarketAsync(Guid marketId, CancellationToken ct);
}

namespace FreshFlow.SharedKernel.Caching;

public interface ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default);
    public Task DeleteAsync(string key, CancellationToken ct = default);
}

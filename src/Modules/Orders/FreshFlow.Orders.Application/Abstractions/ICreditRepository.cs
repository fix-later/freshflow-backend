using FreshFlow.Orders.Domain.Entities;

namespace FreshFlow.Orders.Application.Abstractions;

public interface ICreditRepository
{
    public Task<RestaurantCredit?> FindAccountAsync(Guid restaurantId, CancellationToken ct);
    public Task AddAccountAsync(RestaurantCredit account, CancellationToken ct);
    public void Track(RestaurantCredit account);
    public void AddTransaction(CreditTransaction transaction);
    public Task<IReadOnlyList<CreditTransaction>> GetTransactionsAsync(Guid restaurantId, CancellationToken ct);
    public Task SaveChangesAsync(CancellationToken ct);
}

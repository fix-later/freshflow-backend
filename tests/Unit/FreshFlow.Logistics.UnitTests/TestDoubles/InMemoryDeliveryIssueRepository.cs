using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Domain.Entities;

namespace FreshFlow.Logistics.UnitTests.TestDoubles;

internal sealed class InMemoryDeliveryIssueRepository : IDeliveryIssueRepository
{
    private readonly List<DeliveryIssue> _issues = [];

    public IReadOnlyList<DeliveryIssue> Issues => _issues.AsReadOnly();
    public int SaveChangesCount { get; private set; }

    public Task AddAsync(DeliveryIssue issue, CancellationToken ct)
    {
        _issues.Add(issue);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        SaveChangesCount++;
        return Task.CompletedTask;
    }
}

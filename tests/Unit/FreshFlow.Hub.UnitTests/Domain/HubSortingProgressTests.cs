using FluentAssertions;
using FreshFlow.Hub.Domain.Entities;

namespace FreshFlow.Hub.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class HubSortingProgressTests
{
    [Fact]
    public void Create_ValidArgs_StartsPending()
    {
        var routeId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();

        var line = HubSortingProgress.Create(routeId, orderItemId);

        line.RouteId.Should().Be(routeId);
        line.OrderItemId.Should().Be(orderItemId);
        line.Status.Should().Be(HubSortingProgress.StatusPending);
    }

    [Fact]
    public void MarkSorted_ValidQuantity_TransitionsToSorted()
    {
        var line = HubSortingProgress.Create(Guid.NewGuid(), Guid.NewGuid());
        var userId = Guid.NewGuid();
        var at = DateTime.UtcNow;

        line.MarkSorted(5.5m, userId, at);

        line.Status.Should().Be(HubSortingProgress.StatusSorted);
        line.SortedQuantityKg.Should().Be(5.5m);
        line.SortedByUserId.Should().Be(userId);
        line.SortedAt.Should().Be(at);
    }

    [Fact]
    public void MarkSorted_CalledAgain_OverwritesInsteadOfThrowing()
    {
        var line = HubSortingProgress.Create(Guid.NewGuid(), Guid.NewGuid());
        line.MarkSorted(3m, Guid.NewGuid(), DateTime.UtcNow);
        var secondUserId = Guid.NewGuid();
        var secondAt = DateTime.UtcNow.AddMinutes(1);

        line.MarkSorted(7m, secondUserId, secondAt);

        line.Status.Should().Be(HubSortingProgress.StatusSorted);
        line.SortedQuantityKg.Should().Be(7m);
        line.SortedByUserId.Should().Be(secondUserId);
        line.SortedAt.Should().Be(secondAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MarkSorted_InvalidQuantity_Throws(decimal quantity)
    {
        var line = HubSortingProgress.Create(Guid.NewGuid(), Guid.NewGuid());

        var act = () => line.MarkSorted(quantity, Guid.NewGuid(), DateTime.UtcNow);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}

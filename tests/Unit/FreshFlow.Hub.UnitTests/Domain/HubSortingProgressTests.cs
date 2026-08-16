using FluentAssertions;
using FreshFlow.Hub.Domain.Entities;

namespace FreshFlow.Hub.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class HubSortingProgressTests
{
    [Fact]
    public void Create_ValidArgs_StartsPending()
    {
        var hubId = Guid.NewGuid();
        var serviceDate = new DateOnly(2026, 7, 29);
        var routeId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();

        var line = HubSortingProgress.Create(hubId, serviceDate, orderItemId, routeId);

        line.HubId.Should().Be(hubId);
        line.ServiceDate.Should().Be(serviceDate);
        line.RouteId.Should().Be(routeId);
        line.OrderItemId.Should().Be(orderItemId);
        line.Status.Should().Be(HubSortingProgress.StatusPending);
    }

    [Fact]
    public void UpdateSortedQuantity_FullQuantity_TransitionsToSorted()
    {
        var line = HubSortingProgress.Create(Guid.NewGuid(), new DateOnly(2026, 7, 29), Guid.NewGuid());
        var userId = Guid.NewGuid();
        var at = DateTime.UtcNow;

        line.UpdateSortedQuantity(5.5m, 5.5m, userId, at);

        line.Status.Should().Be(HubSortingProgress.StatusSorted);
        line.SortedQuantityKg.Should().Be(5.5m);
        line.SortedByUserId.Should().Be(userId);
        line.SortedAt.Should().Be(at);
    }

    [Fact]
    public void UpdateSortedQuantity_CalledAgain_StaysPendingUntilRequiredQuantity()
    {
        var line = HubSortingProgress.Create(Guid.NewGuid(), new DateOnly(2026, 7, 29), Guid.NewGuid());
        line.UpdateSortedQuantity(3m, 7m, Guid.NewGuid(), DateTime.UtcNow);
        var secondUserId = Guid.NewGuid();
        var secondAt = DateTime.UtcNow.AddMinutes(1);

        line.UpdateSortedQuantity(7m, 7m, secondUserId, secondAt);

        line.Status.Should().Be(HubSortingProgress.StatusSorted);
        line.SortedQuantityKg.Should().Be(7m);
        line.SortedByUserId.Should().Be(secondUserId);
        line.SortedAt.Should().Be(secondAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void UpdateSortedQuantity_InvalidQuantity_Throws(decimal quantity)
    {
        var line = HubSortingProgress.Create(Guid.NewGuid(), new DateOnly(2026, 7, 29), Guid.NewGuid());

        var act = () => line.UpdateSortedQuantity(quantity, 1m, Guid.NewGuid(), DateTime.UtcNow);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}

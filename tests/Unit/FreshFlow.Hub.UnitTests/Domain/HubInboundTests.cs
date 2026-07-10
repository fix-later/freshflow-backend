using FluentAssertions;
using FreshFlow.Hub.Domain.Entities;

namespace FreshFlow.Hub.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class HubInboundTests
{
    [Fact]
    public void Record_ValidInput_SetsPendingStatusAndTotal()
    {
        var hubId = Guid.NewGuid();
        var item1 = new HubInboundItem(Guid.NewGuid(), Guid.NewGuid(), 10m);
        var item2 = new HubInboundItem(Guid.NewGuid(), null, 15m);

        var inbound = HubInboundEvent.Record(
            hubId,
            Guid.NewGuid(),
            null,
            Guid.NewGuid(),
            [item1, item2],
            DateTime.UtcNow);

        inbound.Id.Should().NotBeEmpty();
        inbound.HubId.Should().Be(hubId);
        inbound.Items.Should().Equal(item1, item2);
        inbound.TotalQuantityKg.Should().Be(25m);
        inbound.Status.Should().Be(HubInboundEvent.StatusPending);
        inbound.ConditionStatus.Should().Be(HubInboundEvent.ConditionOk);
        inbound.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        inbound.UpdatedAt.Should().Be(inbound.CreatedAt);
        inbound.DeletedAt.Should().BeNull();
    }

    [Fact]
    public void Record_EmptyItems_ThrowsArgumentException()
    {
        var act = () => HubInboundEvent.Record(
            Guid.NewGuid(),
            null,
            null,
            null,
            [],
            DateTime.UtcNow);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("items");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Record_InvalidQuantity_ThrowsArgumentException(decimal quantityKg)
    {
        var act = () => HubInboundEvent.Record(
            Guid.NewGuid(),
            null,
            null,
            null,
            [new HubInboundItem(Guid.NewGuid(), null, quantityKg)],
            DateTime.UtcNow);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("items");
    }

    [Fact]
    public void ConfirmArrival_PendingEvent_MarksArrived()
    {
        var inbound = CreateInbound();
        var originalUpdatedAt = inbound.UpdatedAt;

        inbound.ConfirmArrival();

        inbound.Status.Should().Be(HubInboundEvent.StatusArrivedAtHub);
        inbound.UpdatedAt.Should().BeOnOrAfter(originalUpdatedAt);
    }

    [Fact]
    public void ConfirmArrival_AlreadyArrived_ThrowsInvalidOperationException()
    {
        var inbound = CreateInbound();
        inbound.ConfirmArrival();

        var act = inbound.ConfirmArrival;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void HubInventory_AddInbound_IncreasesQuantityIn()
    {
        var inventory = HubInventory.Create(Guid.NewGuid(), Guid.NewGuid());
        var originalUpdatedAt = inventory.UpdatedAt;

        inventory.AddInbound(12m);

        inventory.QuantityIn.Should().Be(12m);
        inventory.QuantityOut.Should().Be(0m);
        inventory.UpdatedAt.Should().BeOnOrAfter(originalUpdatedAt);
    }

    private static HubInboundEvent CreateInbound() =>
        HubInboundEvent.Record(
            Guid.NewGuid(),
            null,
            null,
            null,
            [new HubInboundItem(Guid.NewGuid(), null, 10m)],
            DateTime.UtcNow);
}

using FluentAssertions;
using FreshFlow.Hub.Domain.Entities;

namespace FreshFlow.Hub.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class HubHandoverTests
{
    [Fact]
    public void Create_ValidInput_StartsPendingCheckout()
    {
        var hubId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var driverUserId = Guid.NewGuid();
        var outboundId = Guid.NewGuid();
        var handedOverBy = Guid.NewGuid();

        var result = HubHandoverEvent.Create(
            hubId,
            routeId,
            driverUserId,
            outboundId,
            handedOverBy,
            " Dock 2 ");

        result.Id.Should().NotBeEmpty();
        result.HubId.Should().Be(hubId);
        result.DeliveryRouteId.Should().Be(routeId);
        result.DriverUserId.Should().Be(driverUserId);
        result.OutboundEventId.Should().Be(outboundId);
        result.Status.Should().Be(HubHandoverEvent.StatusPendingCheckout);
        result.HandedOverBy.Should().Be(handedOverBy);
        result.HandedOverAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.DriverConfirmedAt.Should().BeNull();
        result.Notes.Should().Be("Dock 2");
    }

    [Fact]
    public void ConfirmCheckout_AssignedDriver_MarksCheckedOut()
    {
        var driverUserId = Guid.NewGuid();
        var handover = HubHandoverEvent.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            driverUserId,
            null,
            Guid.NewGuid(),
            null);

        handover.ConfirmCheckout(driverUserId);

        handover.Status.Should().Be(HubHandoverEvent.StatusCheckedOut);
        handover.DriverConfirmedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ConfirmCheckout_WrongDriver_Throws()
    {
        var handover = HubHandoverEvent.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            Guid.NewGuid(),
            null);

        var act = () => handover.ConfirmCheckout(Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
        handover.Status.Should().Be(HubHandoverEvent.StatusPendingCheckout);
    }

    [Fact]
    public void ConfirmCheckout_AlreadyCheckedOut_Throws()
    {
        var driverUserId = Guid.NewGuid();
        var handover = HubHandoverEvent.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            driverUserId,
            null,
            Guid.NewGuid(),
            null);
        handover.ConfirmCheckout(driverUserId);

        var act = () => handover.ConfirmCheckout(driverUserId);

        act.Should().Throw<InvalidOperationException>();
    }
}

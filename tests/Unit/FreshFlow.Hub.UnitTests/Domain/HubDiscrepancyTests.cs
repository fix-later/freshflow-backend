using FluentAssertions;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.Hub.Domain.Events;

namespace FreshFlow.Hub.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class HubDiscrepancyTests
{
    [Fact]
    public void Create_ValidArgs_SetsOpenStatusAndRaisesDomainEvent()
    {
        var hubId = Guid.NewGuid();
        var inboundId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();

        var discrepancy = HubDiscrepancy.Create(
            hubId,
            inboundId,
            orderId,
            orderItemId,
            2.5m,
            HubDiscrepancy.ConditionDamaged,
            "Crushed crate");

        discrepancy.Status.Should().Be(HubDiscrepancy.StatusOpen);
        discrepancy.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<HubDiscrepancyRecordedDomainEvent>()
            .Which.Should().Match<HubDiscrepancyRecordedDomainEvent>(e =>
                e.DiscrepancyId == discrepancy.Id &&
                e.HubId == hubId &&
                e.InboundEventId == inboundId &&
                e.OrderId == orderId &&
                e.OrderItemId == orderItemId &&
                e.AffectedQuantity == 2.5m &&
                e.ConditionStatus == HubDiscrepancy.ConditionDamaged);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_InvalidAffectedQuantity_Throws(decimal affectedQuantity)
    {
        var act = () => HubDiscrepancy.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            affectedQuantity,
            HubDiscrepancy.ConditionMissing,
            null);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Acknowledge_OpenDiscrepancy_SetsAcknowledgedFields()
    {
        var adminUserId = Guid.NewGuid();
        var discrepancy = CreateDiscrepancy();

        discrepancy.Acknowledge(adminUserId);

        discrepancy.Status.Should().Be(HubDiscrepancy.StatusAcknowledged);
        discrepancy.AcknowledgedBy.Should().Be(adminUserId);
        discrepancy.AcknowledgedAt.Should().NotBeNull();
    }

    [Fact]
    public void Acknowledge_AlreadyAcknowledged_Throws()
    {
        var discrepancy = CreateDiscrepancy();
        discrepancy.Acknowledge(Guid.NewGuid());

        var act = () => discrepancy.Acknowledge(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    private static HubDiscrepancy CreateDiscrepancy() =>
        HubDiscrepancy.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1m,
            HubDiscrepancy.ConditionMissing,
            null);
}

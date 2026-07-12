using FluentAssertions;
using FreshFlow.Logistics.Domain.Entities;

namespace FreshFlow.Logistics.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class DeliveryTests
{
    [Fact]
    public void Create_ValidInput_ReturnsPendingDelivery()
    {
        var routeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var estimatedArrival = DateTime.UtcNow.AddHours(1);

        var delivery = Delivery.Create(routeId, orderId, 2, estimatedArrival);

        delivery.Id.Should().NotBeEmpty();
        delivery.DeliveryRouteId.Should().Be(routeId);
        delivery.OrderId.Should().Be(orderId);
        delivery.SequenceNumber.Should().Be(2);
        delivery.Status.Should().Be(Delivery.StatusPending);
        delivery.EstimatedArrival.Should().Be(estimatedArrival);
    }

    [Fact]
    public void Create_InvalidIds_ThrowsArgumentException()
    {
        var orderId = Guid.NewGuid();
        var routeId = Guid.NewGuid();

        Action emptyRoute = () => Delivery.Create(Guid.Empty, orderId, 1);
        Action emptyOrder = () => Delivery.Create(routeId, Guid.Empty, 1);

        emptyRoute.Should().Throw<ArgumentException>();
        emptyOrder.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_InvalidSequence_ThrowsArgumentOutOfRangeException(int sequenceNumber)
    {
        Action act = () => Delivery.Create(Guid.NewGuid(), Guid.NewGuid(), sequenceNumber);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MarkArrived_PendingDelivery_SetsArrived()
    {
        var delivery = Delivery.Create(Guid.NewGuid(), Guid.NewGuid(), 1);

        delivery.MarkArrived();

        delivery.Status.Should().Be(Delivery.StatusArrived);
        delivery.ActualArrival.Should().NotBeNull();
    }

    [Fact]
    public void MarkDelivered_ArrivedDelivery_PreservesArrivedTimestamp()
    {
        var delivery = Delivery.Create(Guid.NewGuid(), Guid.NewGuid(), 1);
        delivery.MarkArrived();
        var arrivedAt = delivery.ActualArrival;

        delivery.MarkDelivered(DateTime.UtcNow.AddMinutes(5));

        delivery.Status.Should().Be(Delivery.StatusDelivered);
        delivery.ActualArrival.Should().Be(arrivedAt);
    }

    [Fact]
    public void MarkDelivered_PendingDelivery_SetsActualArrival()
    {
        var delivery = Delivery.Create(Guid.NewGuid(), Guid.NewGuid(), 1);
        var actualArrival = DateTime.UtcNow;

        delivery.MarkDelivered(actualArrival);

        delivery.Status.Should().Be(Delivery.StatusDelivered);
        delivery.ActualArrival.Should().Be(actualArrival);
    }

    [Fact]
    public void MarkFailed_PendingDelivery_SetsFailedReason()
    {
        var delivery = Delivery.Create(Guid.NewGuid(), Guid.NewGuid(), 1);

        delivery.MarkFailed("  customer unavailable ");

        delivery.Status.Should().Be(Delivery.StatusFailed);
        delivery.FailureReason.Should().Be("customer unavailable");
    }

    [Fact]
    public void MarkFailed_EmptyReason_ThrowsArgumentException()
    {
        var delivery = Delivery.Create(Guid.NewGuid(), Guid.NewGuid(), 1);

        Action act = () => delivery.MarkFailed(" ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkDelivered_TerminalDelivery_ThrowsInvalidOperationException()
    {
        var delivered = Delivery.Create(Guid.NewGuid(), Guid.NewGuid(), 1);
        var failed = Delivery.Create(Guid.NewGuid(), Guid.NewGuid(), 2);
        delivered.MarkDelivered(DateTime.UtcNow);
        failed.MarkFailed("failed");

        Action deliverAgain = () => delivered.MarkDelivered(DateTime.UtcNow);
        Action deliverFailed = () => failed.MarkDelivered(DateTime.UtcNow);

        deliverAgain.Should().Throw<InvalidOperationException>();
        deliverFailed.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AttachProof_ValidUrl_TrimsAndSetsProofUrl()
    {
        var delivery = Delivery.Create(Guid.NewGuid(), Guid.NewGuid(), 1);
        var before = delivery.UpdatedAt;

        delivery.AttachProof("  https://res.cloudinary.com/demo/image/upload/pod.jpg  ");

        delivery.ProofUrl.Should().Be("https://res.cloudinary.com/demo/image/upload/pod.jpg");
        delivery.UpdatedAt.Should().BeOnOrAfter(before);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void AttachProof_EmptyUrl_ThrowsArgumentException(string proofUrl)
    {
        var delivery = Delivery.Create(Guid.NewGuid(), Guid.NewGuid(), 1);

        Action act = () => delivery.AttachProof(proofUrl);

        act.Should().Throw<ArgumentException>();
    }
}

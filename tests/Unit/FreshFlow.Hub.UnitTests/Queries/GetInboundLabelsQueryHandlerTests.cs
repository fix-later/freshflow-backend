using FluentAssertions;
using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Application.Queries.GetInboundLabels;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.Hub.UnitTests.TestDoubles;
using NSubstitute;

namespace FreshFlow.Hub.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetInboundLabelsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ExactPackingMultiple_ReturnsOneLabelPerBoxAsync()
    {
        var (sut, inbound, procurement) = await CreateSutAsync(quantityKg: 30m);
        procurement.ReadBatchItemsAsync(inbound.DeliveryScheduleId!.Value, default)
            .Returns([CreatePacking(inbound.Items[0].MarketProductId, 15m)]);

        var result = await sut.Handle(new GetInboundLabelsQuery(inbound.HubId, inbound.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Labels.Should().HaveCount(2);
        result.Value.Labels.Select(label => label.QuantityKg).Should().Equal(15m, 15m);
        result.Value.Labels.Select(label => label.PackageNumber).Should().Equal(1, 2);
        result.Value.Labels.Should().OnlyContain(label => label.PackageCount == 2);
    }

    [Fact]
    public async Task Handle_PartialLastBox_ReturnsRemainderLabelAsync()
    {
        var (sut, inbound, procurement) = await CreateSutAsync(quantityKg: 32m);
        procurement.ReadBatchItemsAsync(inbound.DeliveryScheduleId!.Value, default)
            .Returns([CreatePacking(inbound.Items[0].MarketProductId, 15m)]);

        var result = await sut.Handle(new GetInboundLabelsQuery(inbound.HubId, inbound.Id), default);

        result.Value.Labels.Select(label => label.QuantityKg).Should().Equal(15m, 15m, 2m);
        result.Value.Labels.Should().OnlyContain(label => label.PackageCount == 3);
    }

    [Fact]
    public async Task Handle_MissingPackingCode_ReturnsValidationErrorAsync()
    {
        var (sut, inbound, procurement) = await CreateSutAsync(quantityKg: 30m);
        procurement.ReadBatchItemsAsync(inbound.DeliveryScheduleId!.Value, default)
            .Returns([CreatePacking(inbound.Items[0].MarketProductId, capacityKg: null)]);

        var result = await sut.Handle(new GetInboundLabelsQuery(inbound.HubId, inbound.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PACKING_CODE_MISSING");
    }

    private static async Task<(
        GetInboundLabelsQueryHandler Handler,
        HubInboundEvent Inbound,
        IHubProcurementPlanReader Procurement)> CreateSutAsync(decimal quantityKg)
    {
        var inbounds = new InMemoryHubInboundRepository();
        var procurement = Substitute.For<IHubProcurementPlanReader>();
        var inbound = HubInboundEvent.Record(
            Guid.NewGuid(),
            Guid.NewGuid(),
            deliveryRouteId: null,
            deliveryScheduleId: Guid.NewGuid(),
            [new HubInboundItem(Guid.NewGuid(), null, quantityKg, "Cà chua")],
            DateTime.UtcNow);
        await inbounds.AddAsync(inbound, default);

        return (new GetInboundLabelsQueryHandler(inbounds, procurement), inbound, procurement);
    }

    private static HubProcurementItemDto CreatePacking(Guid marketProductId, decimal? capacityKg) =>
        new(
            marketProductId,
            "Cà chua",
            TargetQuantity: 30,
            ActualQuantity: 30,
            ActualUnitPrice: null,
            PurchasedAt: null,
            ProductId: Guid.NewGuid(),
            PackingCode: capacityKg.HasValue ? "BOX-15" : null,
            PackingCapacityKg: capacityKg);
}

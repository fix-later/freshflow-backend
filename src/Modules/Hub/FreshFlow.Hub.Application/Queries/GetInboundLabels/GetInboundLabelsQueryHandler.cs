using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Hub.Application.Queries.GetInboundLabels;

internal sealed class GetInboundLabelsQueryHandler(
    IHubInboundRepository inbounds,
    IHubProcurementPlanReader procurement)
    : IRequestHandler<GetInboundLabelsQuery, Result<HubInboundLabelsDto>>
{
    public async Task<Result<HubInboundLabelsDto>> Handle(
        GetInboundLabelsQuery request,
        CancellationToken ct)
    {
        var inbound = await inbounds.FindByIdForHubAsync(request.HubId, request.InboundId, ct);
        if (inbound is null)
            return Result<HubInboundLabelsDto>.Failure(Error.NotFound("HUB_INBOUND_EVENT", request.InboundId));

        if (inbound.DeliveryScheduleId is not { } batchId)
            return PackingCodeMissing("Packing data is unavailable for this inbound event.");

        // ponytail: labels use current catalog packing data; persist snapshots only when
        // historical reprints must remain unchanged after catalog edits.
        var packingByProduct = (await procurement.ReadBatchItemsAsync(batchId, ct))
            .ToDictionary(item => item.MarketProductId);
        var labels = new List<HubInboundLabelDto>();

        foreach (var item in inbound.Items)
        {
            if (!packingByProduct.TryGetValue(item.MarketProductId, out var packing) ||
                string.IsNullOrWhiteSpace(packing.PackingCode) ||
                packing.PackingCapacityKg is not { } capacityKg || capacityKg <= 0)
            {
                return PackingCodeMissing(
                    $"Packing code is missing or invalid for '{item.ProductName ?? item.MarketProductId.ToString()}'.");
            }

            var packageCount = checked((int)Math.Ceiling(item.QuantityKg / capacityKg));
            var remainingKg = item.QuantityKg;
            for (var packageNumber = 1; packageNumber <= packageCount; packageNumber++)
            {
                var quantityKg = Math.Min(capacityKg, remainingKg);
                labels.Add(new HubInboundLabelDto(
                    $"{inbound.Id:N}-{item.MarketProductId:N}-{packageNumber}",
                    item.MarketProductId,
                    item.ProductId ?? packing.ProductId,
                    item.ProductName ?? packing.ProductName,
                    packing.PackingCode,
                    capacityKg,
                    packageNumber,
                    packageCount,
                    quantityKg));
                remainingKg -= quantityKg;
            }
        }

        return Result<HubInboundLabelsDto>.Success(new HubInboundLabelsDto(
            inbound.Id,
            inbound.HubId,
            batchId,
            inbound.SourceMarketId,
            labels.AsReadOnly()));
    }

    private static Result<HubInboundLabelsDto> PackingCodeMissing(string message) =>
        Result<HubInboundLabelsDto>.Failure(Error.Validation("PACKING_CODE_MISSING", message));
}

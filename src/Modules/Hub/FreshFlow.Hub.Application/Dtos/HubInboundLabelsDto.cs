namespace FreshFlow.Hub.Application.Dtos;

public sealed record HubInboundLabelsDto(
    Guid InboundId,
    Guid HubId,
    Guid ProcurementBatchId,
    Guid? SourceMarketId,
    IReadOnlyList<HubInboundLabelDto> Labels);

public sealed record HubInboundLabelDto(
    string LabelRef,
    Guid MarketProductId,
    Guid? ProductId,
    string ProductName,
    string PackingCode,
    decimal PackingCapacityKg,
    int PackageNumber,
    int PackageCount,
    decimal QuantityKg);

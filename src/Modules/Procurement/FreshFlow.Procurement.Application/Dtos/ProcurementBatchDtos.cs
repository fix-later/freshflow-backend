using FreshFlow.Procurement.Domain.Entities;

namespace FreshFlow.Procurement.Application.Dtos;

public sealed record ProcurementBatchListDto(
    IReadOnlyList<ProcurementBatchDto> Batches,
    ProcurementBatchPaginationDto Pagination);

public sealed record ProcurementBatchDto(
    Guid Id,
    DateOnly BatchDate,
    Guid MarketId,
    string Status,
    DateTime? ManifestedAt,
    Guid? AssignedAgentUserId,
    DateTime? AssignedAt,
    DateTime? HandedOffAt,
    Guid? HubId,
    int TotalItemCount,
    IReadOnlyList<ProcurementBatchItemDto> Items,
    IReadOnlyList<ProcurementBatchMemberDto> Members);

public sealed record ProcurementBatchItemDto(
    Guid MarketProductId,
    string ProductNameSnapshot,
    int TotalQuantity,
    decimal? ReferenceUnitPrice,
    int? ActualQuantity,
    decimal? ActualUnitPrice,
    DateTime? PurchasedAt);

public sealed record ProcurementBatchMemberDto(
    Guid OrderId,
    string Status);

public sealed record ProcurementBatchPaginationDto(
    int Total,
    int Page,
    int PageSize);

internal static class ProcurementBatchDtoMapper
{
    public static ProcurementBatchDto Map(
        ProcurementBatch batch,
        IReadOnlyDictionary<Guid, string> orderStatuses) =>
        new(
            batch.Id,
            batch.BatchDate,
            batch.MarketId,
            batch.Status.ToString(),
            batch.ManifestedAt,
            batch.AssignedAgentUserId,
            batch.AssignedAt,
            batch.HandedOffAt,
            batch.HubId,
            batch.TotalItemCount,
            batch.Items.Select(item => new ProcurementBatchItemDto(
                    item.MarketProductId,
                    item.ProductNameSnapshot,
                    item.TotalQuantity,
                    item.ReferenceUnitPrice,
                    item.ActualQuantity,
                    item.ActualUnitPrice,
                    item.PurchasedAt))
                .ToList()
                .AsReadOnly(),
            batch.Orders.Select(link => new ProcurementBatchMemberDto(
                    link.OrderId,
                    orderStatuses.GetValueOrDefault(link.OrderId, "Unknown")))
                .ToList()
                .AsReadOnly());
}

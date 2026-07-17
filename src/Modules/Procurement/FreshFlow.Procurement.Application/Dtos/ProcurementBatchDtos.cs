using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;

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
    IReadOnlyList<ProcurementBatchMemberDto> Members,
    IReadOnlyList<ProcurementExceptionDto> Exceptions,
    bool IsCompleted,
    DateTime? CancelledAt,
    string? CancellationReason);

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

public sealed record ProcurementExceptionDto(
    Guid Id,
    Guid MarketProductId,
    string Type,
    int ReportedQuantity,
    string? Note,
    string? ProofImageUrl,
    Guid ReportedByUserId,
    DateTime ReportedAt);

public sealed record ProcurementBatchPaginationDto(
    int Total,
    int Page,
    int PageSize);

internal static class ProcurementBatchDtoMapper
{
    /// <summary>Order statuses that need no further work from a session's point of view.</summary>
    private static readonly string[] SettledOrderStatuses = ["Delivered", "Cancelled"];

    /// <summary>
    /// A session is done once every order it covers is settled — the whole point being that
    /// callers never have to walk the orders themselves. Derived, not stored: the roll-up follows
    /// the orders, which move long after Procurement's own status stops at HandedOff.
    /// </summary>
    private static bool IsCompleted(
        ProcurementBatch batch,
        IReadOnlyDictionary<Guid, string> orderStatuses) =>
        batch.Status == ProcurementBatchStatus.Cancelled ||
        (batch.Orders.Count > 0 && batch.Orders.All(link =>
            SettledOrderStatuses.Contains(orderStatuses.GetValueOrDefault(link.OrderId))));

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
                .AsReadOnly(),
            batch.Exceptions
                .Where(exception => !exception.IsDeleted)
                .Select(exception => new ProcurementExceptionDto(
                    exception.Id,
                    exception.MarketProductId,
                    exception.Type.ToString(),
                    exception.ReportedQuantity,
                    exception.Note,
                    exception.ProofImageUrl,
                    exception.ReportedByUserId,
                    exception.ReportedAt))
                .ToList()
                .AsReadOnly(),
            IsCompleted(batch, orderStatuses),
            batch.CancelledAt,
            batch.CancellationReason);
}

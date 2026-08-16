using FreshFlow.Contracts;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Services;

/// <summary>
/// SCRUM-386 — per due occurrence: build a concrete <see cref="Order"/> from the schedule's item
/// template and auto-confirm it through the same pipeline a manual confirm uses
/// (<see cref="IOrderConfirmationService"/>). A schedule with no item template (pre-SCRUM-386,
/// or any auto-confirm failure — expired address, missing product, credit/stock/cutoff) degrades
/// to the original behavior: an empty (or partially-priced) Draft the restaurant edits by hand.
/// Non-legacy failures publish <see cref="ScheduledOrderNeedsAttentionIntegrationEvent"/> so the
/// restaurant is told why.
/// </summary>
public sealed class ScheduledOrderGenerationService(
    IScheduledOrderRepository scheduledOrderRepository,
    IOrderRepository orderRepository,
    IRestaurantReader restaurantReader,
    IMarketProductReader marketProductReader,
    IOrderConfirmationService orderConfirmationService,
    IPublisher publisher) : IScheduledOrderGenerationService
{
    private const int MaxDueOccurrencesPerSchedule = 366;

    public async Task<ScheduledOrderGenerationResultDto> GenerateDueAsync(
        DateTime utcNow, CancellationToken ct)
    {
        var activeSchedules = await scheduledOrderRepository.GetActiveAsync(ct);
        var createdCount = 0;
        var missedExecutionCount = 0;

        foreach (var schedule in activeSchedules)
        {
            var dueOccurrences = GetDueOccurrences(schedule, utcNow).ToList();
            if (dueOccurrences.Count == 0)
                continue;

            if (dueOccurrences.Count > 1)
                missedExecutionCount += dueOccurrences.Count - 1;

            RoadDistanceResult? roadDistance = null;
            string? roadDistanceFailure = null;
            if (schedule.Items.Count > 0 && schedule.DeliveryAddressId is Guid deliveryAddressId)
            {
                var restaurant = await restaurantReader.FindByIdAsync(schedule.RestaurantId, ct);
                if (restaurant?.IsApproved == true)
                {
                    var result = await orderConfirmationService.GetRoadDistanceAsync(
                        schedule.Items.Select(item => item.MarketProductId).Distinct().ToArray(),
                        schedule.RestaurantId,
                        deliveryAddressId,
                        ct);
                    if (result.IsSuccess)
                        roadDistance = result.Value;
                    else
                        roadDistanceFailure = result.Error.Message;
                }
            }

            foreach (var occurrence in dueOccurrences)
            {
                await GenerateOneAsync(
                    schedule, occurrence, roadDistance, roadDistanceFailure, utcNow, ct);
                createdCount++;
            }
        }

        return new ScheduledOrderGenerationResultDto(
            activeSchedules.Count,
            createdCount,
            missedExecutionCount,
            utcNow);
    }

    /// <summary>
    /// Builds and (attempts to) auto-confirm one occurrence's order, then records the schedule's
    /// execution — all inside one serializable transaction, mirroring the atomicity a manual
    /// confirm gets (order mutation + credit charge commit or roll back together).
    /// </summary>
    private async Task GenerateOneAsync(
        ScheduledOrder schedule,
        DateTime occurrence,
        RoadDistanceResult? roadDistance,
        string? roadDistanceFailure,
        DateTime utcNow,
        CancellationToken ct)
    {
        await orderRepository.ExecuteInSerializableTransactionAsync(async txCt =>
        {
            var order = new Order(
                schedule.RestaurantId, scheduledFor: occurrence, notes: schedule.Notes, scheduledOrderId: schedule.Id);
            await orderRepository.AddAsync(order, txCt);

            // Flush now — the order must have a real row before ConfirmAsync's credit charge
            // inserts a credit_transactions row referencing it. That FK is DB-only (unmodeled in
            // EF, per this module's cross-aggregate convention), so EF can't infer the insert
            // order from the model alone when both are new in the same SaveChanges batch.
            await orderRepository.SaveChangesAsync(txCt);

            var failureReason = await TryConfirmFromTemplateAsync(
                order, schedule, roadDistance, roadDistanceFailure, utcNow, txCt);

            schedule.RecordExecution(occurrence);
            scheduledOrderRepository.Track(schedule);

            if (failureReason is not null)
                await NotifyNeedsAttentionAsync(schedule, order, occurrence, failureReason, txCt);

            return Result.Success();
        }, ct);
    }

    /// <summary>
    /// Returns null on success, or on the "nothing to try yet" legacy degrade (no item template
    /// at all — unchanged pre-SCRUM-386 behavior, no notification). Any other outcome returns a
    /// human-readable reason and leaves <paramref name="order"/> a Draft.
    /// </summary>
    private async Task<string?> TryConfirmFromTemplateAsync(
        Order order,
        ScheduledOrder schedule,
        RoadDistanceResult? roadDistance,
        string? roadDistanceFailure,
        DateTime utcNow,
        CancellationToken ct)
    {
        if (schedule.Items.Count == 0)
            return null;

        if (schedule.DeliveryAddressId is null)
            return "No delivery address is set on this recurring schedule.";

        // Parity with the manual confirm path (ConfirmOrderCommandHandler), which rejects a
        // suspended/unapproved restaurant before ever reaching the pricing/credit pipeline.
        var restaurant = await restaurantReader.FindByIdAsync(schedule.RestaurantId, ct);
        if (restaurant is null || !restaurant.IsApproved)
            return "The restaurant is not approved to place orders.";

        var snapshots = new Dictionary<Guid, MarketProductSnapshotDto>();
        foreach (var item in schedule.Items)
        {
            if (snapshots.ContainsKey(item.MarketProductId))
                continue;

            var snapshot = await marketProductReader.FindAsync(item.MarketProductId, ct);
            if (snapshot is null)
                return $"Product '{item.MarketProductId}' is no longer available.";

            snapshots[item.MarketProductId] = snapshot;
        }

        // order was already flushed (Unchanged) before this call — EF's snapshot change
        // tracking doesn't pick up items added to a field-backed collection on an
        // already-tracked entity on its own, so each new item needs an explicit Added state
        // (same as AddOrderItemCommandHandler.TrackNewItem).
        foreach (var item in schedule.Items)
        {
            var snapshot = snapshots[item.MarketProductId];
            var add = order.AddItem(
                snapshot.MarketProductId,
                snapshot.ProductName,
                item.Quantity,
                snapshot.CurrentPrice,
                snapshot.MarketId,
                snapshot.PackingCode,
                snapshot.PackingWeightKg);
            if (add.IsFailure)
                return add.Error.Message;
            orderRepository.TrackNewItem(order.Items.Last());
        }

        if (roadDistance is null)
            return roadDistanceFailure ?? "Delivery road distance could not be calculated.";

        var confirmResult = await orderConfirmationService.ConfirmAsync(
            order, schedule.RestaurantId, schedule.DeliveryAddressId.Value, roadDistance, utcNow, ct);

        return confirmResult.IsFailure ? confirmResult.Error.Message : null;
    }

    private Task NotifyNeedsAttentionAsync(
        ScheduledOrder schedule, Order order, DateTime occurrence, string reason, CancellationToken ct) =>
        publisher.Publish(
            new ScheduledOrderNeedsAttentionIntegrationEvent(
                schedule.Id, order.Id, schedule.RestaurantId, occurrence, reason, DateTime.UtcNow),
            ct);

    private static IEnumerable<DateTime> GetDueOccurrences(ScheduledOrder schedule, DateTime utcNow)
    {
        var next = schedule.LastExecutedAt.HasValue
            ? NextOccurrence(schedule.LastExecutedAt.Value, schedule.RecurrenceType)
            : schedule.FirstRunAt;

        if (next < schedule.FirstRunAt)
            next = schedule.FirstRunAt;

        for (var i = 0; i < MaxDueOccurrencesPerSchedule && next <= utcNow; i++)
        {
            yield return next;
            next = NextOccurrence(next, schedule.RecurrenceType);
        }
    }

    private static DateTime NextOccurrence(DateTime current, RecurrenceType recurrenceType) =>
        recurrenceType switch
        {
            RecurrenceType.Daily => current.AddDays(1),
            RecurrenceType.Weekly => current.AddDays(7),
            _ => throw new ArgumentOutOfRangeException(nameof(recurrenceType), recurrenceType, null)
        };
}

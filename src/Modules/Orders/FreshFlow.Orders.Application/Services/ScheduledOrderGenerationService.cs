using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;

namespace FreshFlow.Orders.Application.Services;

public sealed class ScheduledOrderGenerationService(
    IScheduledOrderRepository scheduledOrderRepository,
    IOrderRepository orderRepository) : IScheduledOrderGenerationService
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

            foreach (var occurrence in dueOccurrences)
            {
                var order = new Order(
                    schedule.RestaurantId,
                    scheduledFor: occurrence,
                    notes: schedule.Notes,
                    scheduledOrderId: schedule.Id);

                await orderRepository.AddAsync(order, ct);
                schedule.RecordExecution(occurrence);
                createdCount++;
            }

            scheduledOrderRepository.Track(schedule);
        }

        if (createdCount > 0)
            await scheduledOrderRepository.SaveChangesAsync(ct);

        return new ScheduledOrderGenerationResultDto(
            activeSchedules.Count,
            createdCount,
            missedExecutionCount,
            utcNow);
    }

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

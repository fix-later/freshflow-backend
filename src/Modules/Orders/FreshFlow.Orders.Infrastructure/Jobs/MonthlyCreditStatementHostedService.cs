using FreshFlow.Orders.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Orders.Infrastructure.Jobs;

/// <summary>
/// On the 1st of each month (Asia/Ho_Chi_Minh local date — DEC-CRE-03), generates the prior
/// month's credit statement for every restaurant with a credit account. Follows the same
/// polling-BackgroundService shape as <see cref="ScheduledOrderGenerationHostedService"/>;
/// generation itself is delegated to <see cref="ICreditStatementGenerationService"/>
/// (idempotent — a statement already generated for the period is simply returned).
/// </summary>
internal sealed class MonthlyCreditStatementHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<MonthlyCreditStatementHostedService> logger) : BackgroundService
{
    private const int DefaultCheckIntervalMinutes = 60;
    private const int MinimumCheckIntervalMinutes = 15;
    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

    private DateOnly? _lastRunLocalDate;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!ReadBool("Orders:MonthlyCreditStatement:Enabled", defaultValue: true))
        {
            logger.LogInformation("Monthly credit statement job is disabled.");
            return;
        }

        await RunIfDueAsync(stoppingToken);

        var intervalMinutes = Math.Max(
            MinimumCheckIntervalMinutes,
            ReadInt("Orders:MonthlyCreditStatement:CheckIntervalMinutes", DefaultCheckIntervalMinutes));

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunIfDueAsync(stoppingToken);
    }

    private async Task RunIfDueAsync(CancellationToken stoppingToken)
    {
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
        var today = DateOnly.FromDateTime(nowLocal);

        // Only generate once per (VN local) day, and only on the 1st — statements cover
        // the prior calendar month, which has just fully elapsed.
        if (today.Day != 1 || _lastRunLocalDate == today)
            return;

        _lastRunLocalDate = today;
        var priorMonthDate = today.AddDays(-1); // last day of the previous month

        await GenerateAllAsync(priorMonthDate.Year, priorMonthDate.Month, stoppingToken);
    }

    private async Task GenerateAllAsync(int year, int month, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var creditRepository = scope.ServiceProvider.GetRequiredService<ICreditRepository>();
            var generationService = scope.ServiceProvider.GetRequiredService<ICreditStatementGenerationService>();

            var restaurantIds = await creditRepository.GetActiveRestaurantIdsAsync(stoppingToken);
            var succeeded = 0;
            var failed = 0;

            foreach (var restaurantId in restaurantIds)
            {
                var result = await generationService.GenerateAsync(restaurantId, year, month, stoppingToken);
                if (result.IsSuccess)
                {
                    succeeded++;
                    continue;
                }

                failed++;
                logger.LogWarning(
                    "Monthly statement generation failed for restaurant {RestaurantId} period {Year}-{Month}: " +
                    "{ErrorCode} {ErrorMessage}",
                    restaurantId, year, month, result.Error.Code, result.Error.Message);
            }

            logger.LogInformation(
                "Monthly credit statement job completed for {Year}-{Month}: {Succeeded} succeeded, " +
                "{Failed} failed, {Total} total accounts.",
                year, month, succeeded, failed, restaurantIds.Count);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Monthly credit statement job failed.");
        }
    }

    private bool ReadBool(string key, bool defaultValue) =>
        bool.TryParse(configuration[key], out var value) ? value : defaultValue;

    private int ReadInt(string key, int defaultValue) =>
        int.TryParse(configuration[key], out var value) ? value : defaultValue;

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }
}

using FluentAssertions;
using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Infrastructure.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FreshFlow.Notifications.UnitTests.Jobs;

[Trait("Category", "Unit")]
public sealed class NotificationRetryHostedServiceTests
{
    [Fact]
    public async Task StartAsync_EnabledFalse_DoesNotCreateScopeAsync()
    {
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notifications:Retry:Enabled"] = "false",
            })
            .Build();
        var sut = new NotificationRetryHostedService(
            scopeFactory,
            configuration,
            NullLogger<NotificationRetryHostedService>.Instance);

        await sut.StartAsync(default);
        await Task.Delay(50);
        await sut.StopAsync(default);

        scopeFactory.DidNotReceive().CreateScope();
    }

    [Fact]
    public async Task StartAsync_EnabledTrue_RunsRetryOnceWithConfiguredValuesAsync()
    {
        var retryService = new RecordingRetryService(processedCount: 2);
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Notifications:Retry:Enabled"] = "true",
            ["Notifications:Retry:IntervalSeconds"] = "10",
            ["Notifications:Retry:MaxAttempts"] = "7",
            ["Notifications:Retry:BackoffSeconds"] = "30",
            ["Notifications:Retry:BatchSize"] = "12",
        });
        var sut = CreateService(retryService, configuration);

        await sut.StartAsync(default);
        await retryService.WaitUntilCalledAsync();
        await sut.StopAsync(default);

        retryService.MaxAttempts.Should().Be(7);
        retryService.Backoff.Should().Be(TimeSpan.FromSeconds(30));
        retryService.BatchSize.Should().Be(12);
    }

    [Fact]
    public async Task StartAsync_EnabledTrue_UsesDefaultsWhenConfigMissingAsync()
    {
        var retryService = new RecordingRetryService(processedCount: 0);
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Notifications:Retry:Enabled"] = "true",
        });
        var sut = CreateService(retryService, configuration);

        await sut.StartAsync(default);
        await retryService.WaitUntilCalledAsync();
        await sut.StopAsync(default);

        retryService.MaxAttempts.Should().Be(5);
        retryService.Backoff.Should().Be(TimeSpan.FromSeconds(60));
        retryService.BatchSize.Should().Be(50);
    }

    [Fact]
    public async Task StartAsync_RetryThrows_DoesNotThrowAsync()
    {
        var retryService = new RecordingRetryService(
            processedCount: 0,
            exception: new InvalidOperationException("retry failed"));
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Notifications:Retry:Enabled"] = "true",
        });
        var sut = CreateService(retryService, configuration);

        await sut.StartAsync(default);
        await retryService.WaitUntilCalledAsync();
        var act = () => sut.StopAsync(default);

        await act.Should().NotThrowAsync();
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

    private static NotificationRetryHostedService CreateService(
        INotificationRetryService retryService,
        IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddSingleton(retryService);
        var provider = services.BuildServiceProvider();

        return new NotificationRetryHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            configuration,
            NullLogger<NotificationRetryHostedService>.Instance);
    }

    private sealed class RecordingRetryService(
        int processedCount,
        Exception? exception = null) : INotificationRetryService
    {
        private readonly TaskCompletionSource _called =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int? MaxAttempts { get; private set; }
        public TimeSpan? Backoff { get; private set; }
        public int? BatchSize { get; private set; }

        public Task<int> RetryDueAsync(
            int maxAttempts,
            TimeSpan backoff,
            int batchSize,
            CancellationToken ct)
        {
            MaxAttempts = maxAttempts;
            Backoff = backoff;
            BatchSize = batchSize;
            _called.TrySetResult();

            return exception is null
                ? Task.FromResult(processedCount)
                : Task.FromException<int>(exception);
        }

        public async Task WaitUntilCalledAsync() =>
            await _called.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }
}

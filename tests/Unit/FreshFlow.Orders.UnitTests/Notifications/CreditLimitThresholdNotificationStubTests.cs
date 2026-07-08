using FluentAssertions;
using FreshFlow.Contracts;
using FreshFlow.Notifications.Infrastructure;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Orders.UnitTests.Notifications;

[Trait("Category", "Unit")]
public sealed class CreditLimitThresholdNotificationStubTests
{
    [Fact]
    public void AddNotificationsModule_RegistersCreditLimitThresholdIntegrationEventHandler()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotificationsModule(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();

        var handlers = provider
            .GetServices<INotificationHandler<CreditLimitThresholdReachedIntegrationEvent>>()
            .ToList();

        handlers.Should().ContainSingle();
    }

    [Fact]
    public async Task Publish_CreditLimitThresholdIntegrationEvent_LogsStubNotificationAsync()
    {
        var loggerProvider = new CapturingLoggerProvider();
        var restaurantId = Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddProvider(loggerProvider);
        });
        services.AddNotificationsModule(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IPublisher>();

        await publisher.Publish(new CreditLimitThresholdReachedIntegrationEvent(
            restaurantId,
            "warning",
            0.8m,
            80m,
            100m,
            DateTime.UtcNow));

        loggerProvider.Entries.Should().Contain(entry =>
            entry.Level == LogLevel.Warning &&
            entry.Message.Contains("[STUB]", StringComparison.Ordinal) &&
            entry.Message.Contains(restaurantId.ToString(), StringComparison.Ordinal) &&
            entry.Message.Contains("warning", StringComparison.Ordinal));
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly object _gate = new();
        private readonly List<LogEntry> _entries = [];

        public IReadOnlyList<LogEntry> Entries
        {
            get
            {
                lock (_gate)
                {
                    return _entries.ToList().AsReadOnly();
                }
            }
        }

        public ILogger CreateLogger(string categoryName) =>
            new CapturingLogger(categoryName, _entries, _gate);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(
        string categoryName,
        List<LogEntry> entries,
        object gate) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (gate)
            {
                entries.Add(new LogEntry(logLevel, categoryName, formatter(state, exception)));
            }
        }
    }

    private sealed record LogEntry(LogLevel Level, string Category, string Message);
}

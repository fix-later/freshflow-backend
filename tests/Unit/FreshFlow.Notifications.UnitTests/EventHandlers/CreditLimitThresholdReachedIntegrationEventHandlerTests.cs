using FluentAssertions;
using FreshFlow.Contracts;
using FreshFlow.Notifications.Application.EventHandlers;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FreshFlow.Notifications.UnitTests.EventHandlers;

[Trait("Category", "Unit")]
public sealed class CreditLimitThresholdReachedIntegrationEventHandlerTests
{
    [Fact]
    public async Task Handle_CreditLimitThresholdReached_CompletesAsync()
    {
        var logger = Substitute.For<ILogger<CreditLimitThresholdReachedIntegrationEventHandler>>();
        var sut = new CreditLimitThresholdReachedIntegrationEventHandler(logger);
        var notification = new CreditLimitThresholdReachedIntegrationEvent(
            Guid.NewGuid(),
            "warning",
            0.85m,
            85m,
            100m,
            DateTime.UtcNow);

        Func<Task> act = () => sut.Handle(notification, default);

        await act.Should().NotThrowAsync();
    }
}

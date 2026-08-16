using FluentAssertions;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Commands.RunAutoBatch;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using NSubstitute;

namespace FreshFlow.Procurement.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class RunAutoBatchCommandTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 14, 15, 1, 0, TimeSpan.Zero);

    [Fact]
    public void Validator_PastHcmcDate_IsInvalid()
    {
        var validator = new RunAutoBatchCommandValidator(new FixedTimeProvider(Now));

        var result = validator.Validate(
            new RunAutoBatchCommand(new DateOnly(2026, 7, 13), false, false));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Handler_NoTargetDate_UsesDueCycleAsync()
    {
        var service = Substitute.For<IProcurementBatchingService>();
        var date = new DateOnly(2026, 7, 15);
        var session = MarketSession.Create(
            Guid.NewGuid(), Guid.NewGuid(), date,
            new DateTime(2026, 7, 14, 15, 0, 0, DateTimeKind.Utc),
            MarketSessionCreatedSource.Auto, true).Value;
        session.Close(null, null, Now.UtcDateTime);
        var sessions = Substitute.For<IMarketSessionRepository>();
        sessions.ListAsync(date, date, null, MarketSessionStatus.Closed, default).Returns([session]);

        var settings = Substitute.For<IOperationalSettingsReader>();
        settings.ReadAsync(default)
            .Returns(new ProcurementOperationalSettingsDto(true, new TimeOnly(22, 0)));
        service.BuildSessionBatchAsync(session.Id, true, default).Returns(Result<BatchingResult>.Success(
            new BatchingResult(0, 0, 0, false, null, [])));
        var handler = new RunAutoBatchCommandHandler(
            service,
            settings,
            new FixedTimeProvider(Now),
            sessions);

        var result = await handler.Handle(
            new RunAutoBatchCommand(null, true, false),
            default);

        result.IsSuccess.Should().BeTrue();
        await service.Received(1).BuildSessionBatchAsync(session.Id, true, default);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

using FluentAssertions;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Commands.RunAutoBatch;
using FreshFlow.Procurement.Application.Dtos;
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
        var settings = Substitute.For<IOperationalSettingsReader>();
        settings.ReadAsync(default)
            .Returns(new ProcurementOperationalSettingsDto(true, new TimeOnly(22, 0)));
        service.BuildBatchesAsync(
                new DateOnly(2026, 7, 15),
                true,
                false,
                default)
            .Returns(Result<BatchingResult>.Success(
                new BatchingResult(0, 0, 0, false, null, [])));
        var handler = new RunAutoBatchCommandHandler(
            service,
            settings,
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new RunAutoBatchCommand(null, true, false),
            default);

        result.IsSuccess.Should().BeTrue();
        await service.Received(1).BuildBatchesAsync(
            new DateOnly(2026, 7, 15),
            true,
            false,
            default);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

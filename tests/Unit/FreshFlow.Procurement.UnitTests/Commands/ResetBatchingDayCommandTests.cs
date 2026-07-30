using FluentAssertions;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Commands.ResetBatchingDay;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using NSubstitute;

namespace FreshFlow.Procurement.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class ResetBatchingDayCommandTests
{
    private static readonly DateOnly TargetDate = new(2026, 7, 31);
    private static readonly DateTimeOffset Now =
        new(2026, 7, 30, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Validator_RequiresExactDateConfirmation()
    {
        var validator = new ResetBatchingDayCommandValidator();
        var actorId = Guid.NewGuid();

        validator.Validate(new ResetBatchingDayCommand(
                TargetDate,
                "RESET 2026-07-31",
                actorId))
            .IsValid.Should().BeTrue();
        validator.Validate(new ResetBatchingDayCommand(
                TargetDate,
                "reset 2026-07-31",
                actorId))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Handler_Success_ReturnsOperationAndWritesAuditAsync()
    {
        var actorId = Guid.NewGuid();
        var batches = Substitute.For<IProcurementBatchRepository>();
        var auditLogs = Substitute.For<IAuditLogWriter>();
        batches.ResetDayAsync(TargetDate, Now.UtcDateTime, default)
            .Returns(Result<BatchingResetCounts>.Success(new BatchingResetCounts(2, 8)));
        var handler = new ResetBatchingDayCommandHandler(
            batches,
            auditLogs,
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new ResetBatchingDayCommand(TargetDate, "RESET 2026-07-31", actorId),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.OperationId.Should().NotBeEmpty();
        result.Value.Should().BeEquivalentTo(new
        {
            TargetDate,
            BatchesReset = 2,
            OrdersReset = 8
        });
        await auditLogs.Received(1).WriteAsync(
            actorId,
            "procurement_batching_reset",
            "procurement_batch_cycle",
            result.Value.OperationId,
            Arg.Is<string?>(details =>
                details != null &&
                details.Contains("2026-07-31", StringComparison.Ordinal) &&
                details.Contains("\"batchesReset\":2", StringComparison.Ordinal) &&
                details.Contains("\"ordersReset\":8", StringComparison.Ordinal)),
            Now.UtcDateTime,
            default);
    }

    [Fact]
    public async Task Handler_UnsafeState_DoesNotWriteAuditAsync()
    {
        var batches = Substitute.For<IProcurementBatchRepository>();
        var auditLogs = Substitute.For<IAuditLogWriter>();
        batches.ResetDayAsync(TargetDate, Now.UtcDateTime, default)
            .Returns(Result<BatchingResetCounts>.Failure(Error.Conflict(
                "BATCH_RESET_NOT_ALLOWED",
                "unsafe")));
        var handler = new ResetBatchingDayCommandHandler(
            batches,
            auditLogs,
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new ResetBatchingDayCommand(TargetDate, "RESET 2026-07-31", Guid.NewGuid()),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BATCH_RESET_NOT_ALLOWED");
        await auditLogs.DidNotReceiveWithAnyArgs().WriteAsync(
            default,
            string.Empty,
            string.Empty,
            default,
            null,
            default,
            default);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

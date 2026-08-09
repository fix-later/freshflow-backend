using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Commands.UpdateOperationalSettings;
using FreshFlow.Orders.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Orders.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class UpdateOperationalSettingsCommandHandlerTests
{
    private readonly IOperationalSettingsRepository _settings = Substitute.For<IOperationalSettingsRepository>();
    private readonly UpdateOperationalSettingsCommandHandler _sut;

    public UpdateOperationalSettingsCommandHandlerTests() =>
        _sut = new UpdateOperationalSettingsCommandHandler(_settings);

    [Fact]
    public async Task Handle_ValidCommand_UpsertsAndReturnsDto()
    {
        var cutoff = new TimeOnly(21, 30);
        _settings.UpsertAsync(cutoff, false, "direct", 14, 5_000m, 1_000m, 2_000m, 500m,
                Arg.Any<CancellationToken>())
            .Returns(new OperationalSettings(cutoff, false, "direct", 14, 5_000m, 1_000m, 2_000m, 500m));

        var result = await _sut.Handle(new UpdateOperationalSettingsCommand(
            cutoff, false, "direct", 14, 5_000m, 1_000m, 2_000m, 500m), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.DailyCutoffTime.Should().Be(cutoff);
        result.Value.BatchingEnabled.Should().BeFalse();
        result.Value.DefaultRouteType.Should().Be("direct");
        result.Value.DeliveryWindowDays.Should().Be(14);
        result.Value.DeliveryFeePerKm.Should().Be(5_000m);
        result.Value.BaseFee.Should().Be(1_000m);
        result.Value.MinimumFee.Should().Be(2_000m);
        result.Value.RoundingUnit.Should().Be(500m);
        await _settings.Received(1).UpsertAsync(
            cutoff, false, "direct", 14, 5_000m, 1_000m, 2_000m, 500m,
            Arg.Any<CancellationToken>());
    }
}

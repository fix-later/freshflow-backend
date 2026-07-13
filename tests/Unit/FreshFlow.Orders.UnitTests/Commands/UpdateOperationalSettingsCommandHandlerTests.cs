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
        _settings.UpsertAsync(cutoff, false, "direct", Arg.Any<CancellationToken>())
            .Returns(new OperationalSettings(cutoff, false, "direct"));

        var result = await _sut.Handle(new UpdateOperationalSettingsCommand(cutoff, false, "direct"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.DailyCutoffTime.Should().Be(cutoff);
        result.Value.BatchingEnabled.Should().BeFalse();
        result.Value.DefaultRouteType.Should().Be("direct");
        await _settings.Received(1).UpsertAsync(cutoff, false, "direct", Arg.Any<CancellationToken>());
    }
}

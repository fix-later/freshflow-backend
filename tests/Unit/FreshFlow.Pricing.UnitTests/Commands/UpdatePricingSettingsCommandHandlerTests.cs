using FluentAssertions;
using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Commands.UpdatePricingSettings;
using FreshFlow.Pricing.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Pricing.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class UpdatePricingSettingsCommandHandlerTests
{
    private readonly IPricingSettingsRepository _settings = Substitute.For<IPricingSettingsRepository>();
    private readonly UpdatePricingSettingsCommandHandler _sut;

    public UpdatePricingSettingsCommandHandlerTests() =>
        _sut = new UpdatePricingSettingsCommandHandler(_settings);

    [Fact]
    public async Task Handle_ValidCommand_UpsertsAndReturnsDto()
    {
        _settings.UpsertAsync(15.00m, Arg.Any<CancellationToken>())
            .Returns(new PricingSettings(15.00m));

        var result = await _sut.Handle(new UpdatePricingSettingsCommand(15.00m), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.PriceAlertThresholdPercent.Should().Be(15.00m);
        await _settings.Received(1).UpsertAsync(15.00m, Arg.Any<CancellationToken>());
    }
}

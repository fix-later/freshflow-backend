using FluentAssertions;
using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Queries.GetPricingSettings;
using FreshFlow.Pricing.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Pricing.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetPricingSettingsQueryHandlerTests
{
    private readonly IPricingSettingsRepository _settings = Substitute.For<IPricingSettingsRepository>();
    private readonly GetPricingSettingsQueryHandler _sut;

    public GetPricingSettingsQueryHandlerTests() =>
        _sut = new GetPricingSettingsQueryHandler(_settings);

    [Fact]
    public async Task Handle_NoRowPersisted_ReturnsDefault()
    {
        _settings.GetAsync(Arg.Any<CancellationToken>()).Returns(PricingSettings.CreateDefault());

        var result = await _sut.Handle(new GetPricingSettingsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.PriceAlertThresholdPercent.Should().Be(10.00m);
    }
}

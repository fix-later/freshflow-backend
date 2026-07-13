using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Queries.GetOperationalSettings;
using FreshFlow.Orders.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Orders.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetOperationalSettingsQueryHandlerTests
{
    private readonly IOperationalSettingsRepository _settings = Substitute.For<IOperationalSettingsRepository>();
    private readonly GetOperationalSettingsQueryHandler _sut;

    public GetOperationalSettingsQueryHandlerTests() =>
        _sut = new GetOperationalSettingsQueryHandler(_settings);

    [Fact]
    public async Task Handle_NoRowPersisted_ReturnsDefault()
    {
        _settings.GetAsync(Arg.Any<CancellationToken>()).Returns(OperationalSettings.CreateDefault());

        var result = await _sut.Handle(new GetOperationalSettingsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.DailyCutoffTime.Should().Be(new TimeOnly(22, 0));
        result.Value.BatchingEnabled.Should().BeTrue();
        result.Value.DefaultRouteType.Should().Be("hub_relay");
    }
}

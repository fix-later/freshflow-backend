using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Commands.SettleRestaurantCredit;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using NSubstitute;

namespace FreshFlow.Orders.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class SettleRestaurantCreditCommandHandlerTests
{
    private readonly ICreditService _creditService = Substitute.For<ICreditService>();
    private readonly SettleRestaurantCreditCommandHandler _sut;

    private static readonly Guid RestaurantId = Guid.NewGuid();

    public SettleRestaurantCreditCommandHandlerTests()
    {
        _sut = new SettleRestaurantCreditCommandHandler(_creditService);
    }

    [Fact]
    public async Task Handle_ServiceFailure_PropagatesErrorAsync()
    {
        _creditService.SettleAsync(RestaurantId, 100m, "payment", default)
            .Returns(Result<RestaurantCreditDto>.Failure(
                Error.Validation("CREDIT_SETTLEMENT_EXCEEDS_BALANCE", "Settlement amount cannot exceed outstanding balance.")));

        var result = await _sut.Handle(new SettleRestaurantCreditCommand(RestaurantId, 100m, "payment"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CREDIT_SETTLEMENT_EXCEEDS_BALANCE");
    }

    [Fact]
    public async Task Handle_ServiceSuccess_ReturnsUpdatedCreditAsync()
    {
        var dto = new RestaurantCreditDto(RestaurantId, 1_000m, 200m, 800m, DateTime.UtcNow);
        _creditService.SettleAsync(RestaurantId, 300m, "payment", default)
            .Returns(Result<RestaurantCreditDto>.Success(dto));

        var result = await _sut.Handle(new SettleRestaurantCreditCommand(RestaurantId, 300m, "payment"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(dto);
    }
}

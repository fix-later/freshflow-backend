using FluentAssertions;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Commands.UpdateMyTaxProfile;
using FreshFlow.Auth.Domain.Enums;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class UpdateMyTaxProfileCommandHandlerTests
{
    private readonly IRestaurantRepository _restaurants = Substitute.For<IRestaurantRepository>();
    private readonly UpdateMyTaxProfileCommandHandler _sut;

    public UpdateMyTaxProfileCommandHandlerTests() =>
        _sut = new UpdateMyTaxProfileCommandHandler(_restaurants);

    [Fact]
    public async Task Handle_NoRestaurant_ReturnsNotFoundAsync()
    {
        var userId = Guid.NewGuid();
        _restaurants.FindByUserIdAsync(userId, default).Returns((RestaurantDto?)null);

        var result = await _sut.Handle(
            new UpdateMyTaxProfileCommand(
                userId, "0312345678", "FreshFlow Co.", "123 Nguyễn Huệ", "invoice@freshflow.vn"),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_ValidCommand_UpdatesAndEchoesTaxProfileAsync()
    {
        var userId = Guid.NewGuid();
        var restaurantId = Guid.NewGuid();
        var existing = new RestaurantDto(
            restaurantId, "FreshFlow", RestaurantStatus.Active, DateTime.UtcNow, userId);
        var updated = existing with
        {
            TaxCode = "0312345678",
            InvoiceLegalName = "FreshFlow Co.",
            InvoiceAddress = "123 Nguyễn Huệ",
            InvoiceEmail = "invoice@freshflow.vn"
        };
        _restaurants.FindByUserIdAsync(userId, default).Returns(existing);
        _restaurants.UpdateTaxProfileAsync(
            restaurantId,
            "0312345678",
            "FreshFlow Co.",
            "123 Nguyễn Huệ",
            "invoice@freshflow.vn",
            default).Returns(updated);

        var result = await _sut.Handle(
            new UpdateMyTaxProfileCommand(
                userId, "0312345678", "FreshFlow Co.", "123 Nguyễn Huệ", "invoice@freshflow.vn"),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(restaurantId);
        result.Value.TaxCode.Should().Be("0312345678");
        result.Value.LegalName.Should().Be("FreshFlow Co.");
        result.Value.Address.Should().Be("123 Nguyễn Huệ");
        result.Value.Email.Should().Be("invoice@freshflow.vn");
        await _restaurants.Received(1).UpdateTaxProfileAsync(
            restaurantId,
            "0312345678",
            "FreshFlow Co.",
            "123 Nguyễn Huệ",
            "invoice@freshflow.vn",
            default);
    }
}

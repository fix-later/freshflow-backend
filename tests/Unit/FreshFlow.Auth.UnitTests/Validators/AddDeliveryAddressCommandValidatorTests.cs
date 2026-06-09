using FluentAssertions;
using FreshFlow.Auth.Application.Commands.DeliveryAddress.Add;

namespace FreshFlow.Auth.UnitTests.Validators;

[Trait("Category", "Unit")]
public sealed class AddDeliveryAddressCommandValidatorTests
{
    private readonly AddDeliveryAddressCommandValidator _sut = new();
    private static readonly Guid AnyUserId = Guid.NewGuid();

    private static AddDeliveryAddressCommand ValidCommand(
        string addressLine = "123 Main St",
        string? recipientName = null,
        string? phone = null,
        decimal? latitude = null,
        decimal? longitude = null,
        bool isDefault = false) =>
        new(AnyUserId, recipientName, phone, addressLine, latitude, longitude, isDefault);

    [Fact]
    public async Task Validate_ValidFullCommand_Passes()
    {
        var result = await _sut.ValidateAsync(ValidCommand(
            "123 Nguyen Hue, Q1, HCMC", "John Doe", "+84901234567",
            10.762622m, 106.660172m, true));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_ValidMinimalCommand_Passes()
    {
        var result = await _sut.ValidateAsync(ValidCommand());

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_EmptyOrWhitespaceAddressLine_Fails(string addressLine)
    {
        var result = await _sut.ValidateAsync(ValidCommand(addressLine));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(AddDeliveryAddressCommand.AddressLine));
    }

    [Fact]
    public async Task Validate_AddressLineTooLong_Fails()
    {
        var result = await _sut.ValidateAsync(ValidCommand(new string('x', 501)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(AddDeliveryAddressCommand.AddressLine));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("123")]
    [InlineData("not-a-phone")]
    public async Task Validate_InvalidPhone_Fails(string phone)
    {
        var result = await _sut.ValidateAsync(ValidCommand(phone: phone));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(AddDeliveryAddressCommand.Phone));
    }

    [Fact]
    public async Task Validate_NullPhone_Passes()
    {
        var result = await _sut.ValidateAsync(ValidCommand(phone: null));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(-91)]
    [InlineData(91)]
    public async Task Validate_InvalidLatitude_Fails(double lat)
    {
        var result = await _sut.ValidateAsync(ValidCommand(latitude: (decimal)lat));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(AddDeliveryAddressCommand.Latitude));
    }

    [Theory]
    [InlineData(-181)]
    [InlineData(181)]
    public async Task Validate_InvalidLongitude_Fails(double lon)
    {
        var result = await _sut.ValidateAsync(ValidCommand(longitude: (decimal)lon));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(AddDeliveryAddressCommand.Longitude));
    }

    [Fact]
    public async Task Validate_BoundaryLatitudeLongitude_Passes()
    {
        var result = await _sut.ValidateAsync(ValidCommand(
            latitude: -90m, longitude: 180m));

        result.IsValid.Should().BeTrue();
    }
}

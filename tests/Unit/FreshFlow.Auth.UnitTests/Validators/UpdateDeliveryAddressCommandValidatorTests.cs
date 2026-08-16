using FluentAssertions;
using FreshFlow.Auth.Application.Commands.DeliveryAddress.Update;

namespace FreshFlow.Auth.UnitTests.Validators;

[Trait("Category", "Unit")]
public sealed class UpdateDeliveryAddressCommandValidatorTests
{
    private readonly UpdateDeliveryAddressCommandValidator _sut = new();
    private static readonly Guid AnyUserId = Guid.NewGuid();
    private static readonly Guid AnyAddressId = Guid.NewGuid();

    private static UpdateDeliveryAddressCommand ValidCommand(
        string addressLine = "123 Main St",
        string? phone = null,
        decimal? latitude = null,
        decimal? longitude = null) =>
        new(AnyUserId, AnyAddressId, null, phone, addressLine, latitude, longitude, false);

    [Fact]
    public async Task Validate_ValidFullCommand_Passes()
    {
        var result = await _sut.ValidateAsync(
            new UpdateDeliveryAddressCommand(
                AnyUserId, AnyAddressId, "Jane", "+84901234567",
                "New address", 10.762622m, 106.660172m, true));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_EmptyAddressLine_Fails(string addressLine)
    {
        var result = await _sut.ValidateAsync(ValidCommand(addressLine));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(UpdateDeliveryAddressCommand.AddressLine));
    }

    [Fact]
    public async Task Validate_AddressLineTooLong_Fails()
    {
        var result = await _sut.ValidateAsync(ValidCommand(new string('y', 501)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(UpdateDeliveryAddressCommand.AddressLine));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12")]
    public async Task Validate_InvalidPhone_Fails(string phone)
    {
        var result = await _sut.ValidateAsync(ValidCommand(phone: phone));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(UpdateDeliveryAddressCommand.Phone));
    }

    [Theory]
    [InlineData(-91.0)]
    [InlineData(91.0)]
    public async Task Validate_InvalidLatitude_Fails(double lat)
    {
        var result = await _sut.ValidateAsync(ValidCommand(latitude: (decimal)lat));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(UpdateDeliveryAddressCommand.Latitude));
    }

    [Theory]
    [InlineData(-181.0)]
    [InlineData(181.0)]
    public async Task Validate_InvalidLongitude_Fails(double lon)
    {
        var result = await _sut.ValidateAsync(ValidCommand(longitude: (decimal)lon));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(UpdateDeliveryAddressCommand.Longitude));
    }
}

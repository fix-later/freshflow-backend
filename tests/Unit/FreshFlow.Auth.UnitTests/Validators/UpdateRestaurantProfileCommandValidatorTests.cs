using FluentAssertions;
using FreshFlow.Auth.Application.Commands.UpdateRestaurantProfile;

namespace FreshFlow.Auth.UnitTests.Validators;

[Trait("Category", "Unit")]
public sealed class UpdateRestaurantProfileCommandValidatorTests
{
    private readonly UpdateRestaurantProfileCommandValidator _sut = new();
    private static readonly Guid AnyUserId = Guid.NewGuid();

    private static UpdateRestaurantProfileCommand ValidCommand(
        string name = "Phở Bà Tú",
        string? address = null,
        string? contactPerson = null,
        TimeOnly? pickupStart = null,
        TimeOnly? pickupEnd = null) =>
        new(AnyUserId, name, address, contactPerson, pickupStart, pickupEnd);

    [Fact]
    public async Task Validate_ValidFullCommand_Passes()
    {
        var result = await _sut.ValidateAsync(ValidCommand(
            "My Restaurant",
            "123 Nguyen Hue, Q1, HCMC",
            "Nguyen Van A",
            new TimeOnly(8, 0),
            new TimeOnly(12, 0)));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_ValidMinimalCommand_Passes()
    {
        var result = await _sut.ValidateAsync(ValidCommand("Just a name"));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_EmptyOrWhitespaceName_Fails(string name)
    {
        var result = await _sut.ValidateAsync(ValidCommand(name));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateRestaurantProfileCommand.Name));
    }

    [Fact]
    public async Task Validate_NameExceeds200Chars_Fails()
    {
        var result = await _sut.ValidateAsync(ValidCommand(new string('A', 201)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateRestaurantProfileCommand.Name));
    }

    [Fact]
    public async Task Validate_NameAt200Chars_Passes()
    {
        var result = await _sut.ValidateAsync(ValidCommand(new string('A', 200)));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_AddressExceeds500Chars_Fails()
    {
        var result = await _sut.ValidateAsync(ValidCommand(address: new string('x', 501)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateRestaurantProfileCommand.Address));
    }

    [Fact]
    public async Task Validate_AddressAt500Chars_Passes()
    {
        var result = await _sut.ValidateAsync(ValidCommand(address: new string('x', 500)));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_ContactPersonExceeds200Chars_Fails()
    {
        var result = await _sut.ValidateAsync(ValidCommand(contactPerson: new string('c', 201)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(UpdateRestaurantProfileCommand.ContactPerson));
    }

    [Fact]
    public async Task Validate_ContactPersonAt200Chars_Passes()
    {
        var result = await _sut.ValidateAsync(ValidCommand(contactPerson: new string('c', 200)));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_PickupEndBeforeStart_Fails()
    {
        var result = await _sut.ValidateAsync(ValidCommand(
            pickupStart: new TimeOnly(12, 0),
            pickupEnd: new TimeOnly(8, 0)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(UpdateRestaurantProfileCommand.PickupEnd));
    }

    [Fact]
    public async Task Validate_PickupEndEqualToStart_Fails()
    {
        var result = await _sut.ValidateAsync(ValidCommand(
            pickupStart: new TimeOnly(10, 0),
            pickupEnd: new TimeOnly(10, 0)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(UpdateRestaurantProfileCommand.PickupEnd));
    }

    [Fact]
    public async Task Validate_PickupEndAfterStart_Passes()
    {
        var result = await _sut.ValidateAsync(ValidCommand(
            pickupStart: new TimeOnly(8, 0),
            pickupEnd: new TimeOnly(12, 0)));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_PickupStartProvidedWithoutEnd_Passes()
    {
        // Cross-field rule only fires when both are provided
        var result = await _sut.ValidateAsync(ValidCommand(
            pickupStart: new TimeOnly(8, 0),
            pickupEnd: null));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_PickupEndProvidedWithoutStart_Passes()
    {
        // Cross-field rule only fires when both are provided
        var result = await _sut.ValidateAsync(ValidCommand(
            pickupStart: null,
            pickupEnd: new TimeOnly(12, 0)));

        result.IsValid.Should().BeTrue();
    }
}

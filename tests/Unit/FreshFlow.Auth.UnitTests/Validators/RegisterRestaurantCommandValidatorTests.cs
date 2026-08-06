using FluentAssertions;
using FreshFlow.Auth.Application.Commands.RegisterRestaurant;

namespace FreshFlow.Auth.UnitTests.Validators;

[Trait("Category", "Unit")]
public sealed class RegisterRestaurantCommandValidatorTests
{
    private readonly RegisterRestaurantCommandValidator _sut = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    public async Task Validate_InvalidEmail_Fails(string email)
    {
        var result = await _sut.ValidateAsync(
            new RegisterRestaurantCommand(email, "ValidP@ss1", "My Restaurant", null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterRestaurantCommand.Email));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("nouppercase1!")]
    [InlineData("NoDigit!!")]
    [InlineData("NoSpecial1")]
    public async Task Validate_WeakOrEmptyPassword_Fails(string password)
    {
        var result = await _sut.ValidateAsync(
            new RegisterRestaurantCommand("u@test.com", password, "My Restaurant", null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterRestaurantCommand.Password));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_EmptyRestaurantName_Fails(string restaurantName)
    {
        var result = await _sut.ValidateAsync(
            new RegisterRestaurantCommand("u@test.com", "ValidP@ss1", restaurantName, null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterRestaurantCommand.RestaurantName));
    }

    [Theory]
    [InlineData("not-a-phone")]
    [InlineData("123")]
    [InlineData("abc")]
    public async Task Validate_InvalidPhone_Fails(string phone)
    {
        var result = await _sut.ValidateAsync(
            new RegisterRestaurantCommand("u@test.com", "ValidP@ss1", "My Restaurant", phone));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterRestaurantCommand.Phone));
    }

    [Theory]
    [InlineData(" +84901234567")]
    [InlineData("+84901234567 ")]
    [InlineData(" +84901234567 ")]
    public async Task Validate_PhoneWithWhitespace_PassesAfterTrim(string phone)
    {
        var result = await _sut.ValidateAsync(
            new RegisterRestaurantCommand("u@test.com", "ValidP@ss1", "My Restaurant", phone));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_ValidCommandWithPhone_Passes()
    {
        var result = await _sut.ValidateAsync(
            new RegisterRestaurantCommand(
                "owner@phobaatu.vn", "MySecureP@ss1", "Phở Bà Tú", "+84901234567"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_ValidCommandWithoutPhone_Passes()
    {
        var result = await _sut.ValidateAsync(
            new RegisterRestaurantCommand(
                "owner@test.com", "ValidP@ss1!", "My Restaurant", null));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("0312345678")]
    [InlineData("0312345678-001")]
    public async Task Validate_ValidTaxCode_Passes(string taxCode)
    {
        var result = await _sut.ValidateAsync(
            new RegisterRestaurantCommand(
                "owner@test.com", "ValidP@ss1!", "My Restaurant", null, taxCode));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("123")]
    [InlineData("abcdefghij")]
    [InlineData("0312345678-01")]
    [InlineData("03123456789")]
    public async Task Validate_InvalidTaxCode_Fails(string taxCode)
    {
        var result = await _sut.ValidateAsync(
            new RegisterRestaurantCommand(
                "owner@test.com", "ValidP@ss1!", "My Restaurant", null, taxCode));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterRestaurantCommand.TaxCode));
    }

    [Fact]
    public async Task Validate_NullTaxCode_Passes()
    {
        var result = await _sut.ValidateAsync(
            new RegisterRestaurantCommand(
                "owner@test.com", "ValidP@ss1!", "My Restaurant", null, null));
        result.IsValid.Should().BeTrue();
    }
}

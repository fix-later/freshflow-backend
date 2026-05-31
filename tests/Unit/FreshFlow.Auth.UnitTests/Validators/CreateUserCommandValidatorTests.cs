using FluentAssertions;
using FreshFlow.Auth.Application.Commands.Admin.CreateUser;

namespace FreshFlow.Auth.UnitTests.Validators;

[Trait("Category", "Unit")]
public sealed class CreateUserCommandValidatorTests
{
    private readonly CreateUserCommandValidator _sut = new();

    [Theory]
    [InlineData("")]
    [InlineData("not-email")]
    public async Task Validate_InvalidEmail_Fails(string email)
    {
        var result = await _sut.ValidateAsync(
            new CreateUserCommand(email, "ValidP@ss1", "driver", null, null));
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("short")]
    [InlineData("nouppercase1!")]
    [InlineData("NoDigit!!")]
    [InlineData("NoSpecial1")]
    public async Task Validate_WeakPassword_Fails(string password)
    {
        var result = await _sut.ValidateAsync(
            new CreateUserCommand("u@test.com", password, "driver", null, null));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_MarketAgentWithoutMarketId_Fails()
    {
        var result = await _sut.ValidateAsync(
            new CreateUserCommand("u@test.com", "ValidP@ss1!", "market_agent", null, null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateUserCommand.MarketId));
    }

    [Fact]
    public async Task Validate_RestaurantWithoutName_Fails()
    {
        var result = await _sut.ValidateAsync(
            new CreateUserCommand("u@test.com", "ValidP@ss1!", "restaurant", null, null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateUserCommand.RestaurantName));
    }

    [Fact]
    public async Task Validate_DriverWithValidPassword_Passes()
    {
        var result = await _sut.ValidateAsync(
            new CreateUserCommand("u@test.com", "ValidP@ss1!", "driver", null, null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_KioskStaffAlias_Passes()
    {
        var result = await _sut.ValidateAsync(
            new CreateUserCommand("u@test.com", "ValidP@ss1!", "kiosk_staff", Guid.NewGuid(), null));
        result.IsValid.Should().BeTrue();
    }
}

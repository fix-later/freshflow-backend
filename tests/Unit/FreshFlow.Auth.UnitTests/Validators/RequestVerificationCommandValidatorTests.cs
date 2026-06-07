using FluentAssertions;
using FreshFlow.Auth.Application.Commands.RequestVerification;

namespace FreshFlow.Auth.UnitTests.Validators;

[Trait("Category", "Unit")]
public sealed class RequestVerificationCommandValidatorTests
{
    private readonly RequestVerificationCommandValidator _sut = new();

    [Theory]
    [InlineData("", "EMAIL")]
    [InlineData("not-an-email", "EMAIL")]
    [InlineData("u@test.com", "")]
    public async Task Validate_InvalidInput_Fails(string identifier, string channel)
    {
        var result = await _sut.ValidateAsync(new RequestVerificationCommand(identifier, channel));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_ValidEmailAndChannel_Passes()
    {
        var result = await _sut.ValidateAsync(new RequestVerificationCommand("u@test.com", "EMAIL"));
        result.IsValid.Should().BeTrue();
    }
}

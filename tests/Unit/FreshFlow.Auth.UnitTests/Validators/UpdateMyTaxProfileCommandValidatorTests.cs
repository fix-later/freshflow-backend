using FluentAssertions;
using FreshFlow.Auth.Application.Commands.UpdateMyTaxProfile;

namespace FreshFlow.Auth.UnitTests.Validators;

[Trait("Category", "Unit")]
public sealed class UpdateMyTaxProfileCommandValidatorTests
{
    private readonly UpdateMyTaxProfileCommandValidator _sut = new();

    private static UpdateMyTaxProfileCommand Command(
        string taxCode = "0312345678",
        string legalName = "Công ty TNHH FreshFlow",
        string? address = "123 Nguyễn Huệ, Quận 1, TP.HCM",
        string? email = "invoice@freshflow.vn") =>
        new(Guid.NewGuid(), taxCode, legalName, address, email);

    [Theory]
    [InlineData("0312345678")]
    [InlineData("0312345678-001")]
    public async Task Validate_ValidTaxCode_PassesAsync(string taxCode)
    {
        var result = await _sut.ValidateAsync(Command(taxCode));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("123456789")]
    [InlineData("ABCDEFGHIJ")]
    public async Task Validate_InvalidTaxCode_FailsAsync(string taxCode)
    {
        var result = await _sut.ValidateAsync(Command(taxCode));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_BlankLegalName_FailsAsync()
    {
        var result = await _sut.ValidateAsync(Command(legalName: " "));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_BlankAddress_FailsAsync()
    {
        var result = await _sut.ValidateAsync(Command(address: " "));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_InvalidEmail_FailsAsync()
    {
        var result = await _sut.ValidateAsync(Command(email: "not-an-email"));

        result.IsValid.Should().BeFalse();
    }
}

using FluentAssertions;
using FreshFlow.Invoicing.Domain.Validation;

namespace FreshFlow.Invoicing.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class InvoiceBuyerValidatorTests
{
    [Theory]
    [InlineData("0312345678")]
    [InlineData("0312345678-001")]
    public void VietnameseMst_ValidShapes_AreAccepted(string taxCode)
    {
        VietnameseTaxCodeValidator.IsValid(taxCode).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("031234567")]
    [InlineData("03123456789")]
    [InlineData("0312345678001")]
    [InlineData("0312345678_001")]
    [InlineData("0312345678-01")]
    [InlineData("0312345678-0001")]
    [InlineData("031234567A")]
    [InlineData("０３１２３４５６７８")]
    public void VietnameseMst_InvalidShapes_AreRejected(string? taxCode)
    {
        VietnameseTaxCodeValidator.IsValid(taxCode).Should().BeFalse();
    }

    [Theory]
    [InlineData(" ", "Company", "Address", "BUYER_TAX_CODE_REQUIRED")]
    [InlineData("031234567A", "Company", "Address", "BUYER_TAX_CODE_INVALID")]
    [InlineData("0312345678", " ", "Address", "BUYER_LEGAL_NAME_REQUIRED")]
    [InlineData("0312345678", "Company", null, "BUYER_ADDRESS_REQUIRED")]
    public void BuyerProfile_InvalidField_ReturnsSpecificReason(
        string? taxCode, string? legalName, string? address, string expected)
    {
        InvoiceBuyerValidator.GetErrorCode(taxCode, legalName, address).Should().Be(expected);
    }
}

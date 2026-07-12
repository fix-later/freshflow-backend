using FluentAssertions;
using FreshFlow.Logistics.Application.Commands.ReportDeliveryIssue;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class ReportDeliveryIssueCommandValidatorTests
{
    private readonly ReportDeliveryIssueCommandValidator _sut = new();

    [Theory]
    [InlineData("undeliverable")]
    [InlineData("DAMAGED")]
    [InlineData("Customer_Rejected")]
    [InlineData("other")]
    public void Validate_AllowedIssueTypes_Passes(string issueType)
    {
        var result = _sut.Validate(new ReportDeliveryIssueCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            issueType,
            "issue"));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("missing")]
    public void Validate_InvalidIssueType_Fails(string issueType)
    {
        var result = _sut.Validate(new ReportDeliveryIssueCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            issueType,
            "issue"));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_EmptyDescription_Fails(string description)
    {
        var result = _sut.Validate(new ReportDeliveryIssueCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "damaged",
            description));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_DescriptionTooLong_Fails()
    {
        var result = _sut.Validate(new ReportDeliveryIssueCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "damaged",
            new string('x', 1001)));

        result.IsValid.Should().BeFalse();
    }
}

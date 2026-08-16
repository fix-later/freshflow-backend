using FluentAssertions;
using FreshFlow.Hub.Application.Commands.RecordDiscrepancy;
using FreshFlow.Hub.Domain.Entities;

namespace FreshFlow.Hub.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class HubDiscrepancyProofValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("https://res.cloudinary.com/demo/image/upload/proof.jpg")]
    public void OptionalCloudinaryProofUrl_Passes(string? proofUrl)
    {
        var result = new RecordDiscrepancyCommandValidator().Validate(Command(proofUrl));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("http://res.cloudinary.com/demo/image/upload/proof.jpg")]
    [InlineData("https://example.com/proof.jpg")]
    public void NonCloudinaryHttpsProofUrl_Fails(string proofUrl)
    {
        var result = new RecordDiscrepancyCommandValidator().Validate(Command(proofUrl));

        result.Errors.Should().Contain(error =>
            error.PropertyName == nameof(RecordDiscrepancyCommand.ProofImageUrl));
    }

    [Fact]
    public void ProofUrlLongerThan512_Fails()
    {
        var proofUrl = "https://res.cloudinary.com/" + new string('a', 512);

        var result = new RecordDiscrepancyCommandValidator().Validate(Command(proofUrl));

        result.Errors.Should().Contain(error =>
            error.PropertyName == nameof(RecordDiscrepancyCommand.ProofImageUrl));
    }

    private static RecordDiscrepancyCommand Command(string? proofUrl) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1m,
            HubDiscrepancy.ConditionDamaged,
            null,
            ProofImageUrl: proofUrl);
}

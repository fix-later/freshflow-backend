using FluentAssertions;
using FreshFlow.Logistics.Application.Commands.AttachProofOfDelivery;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class AttachProofOfDeliveryCommandValidatorTests
{
    private readonly AttachProofOfDeliveryCommandValidator _sut = new();

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_EmptyProofUrl_Fails(string proofUrl)
    {
        var result = _sut.Validate(new AttachProofOfDeliveryCommand(Guid.NewGuid(), Guid.NewGuid(), proofUrl));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ProofUrlTooLong_Fails()
    {
        var proofUrl = "https://res.cloudinary.com/" + new string('a', 512);

        var result = _sut.Validate(new AttachProofOfDeliveryCommand(Guid.NewGuid(), Guid.NewGuid(), proofUrl));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_NonHttpsProofUrl_Fails()
    {
        var result = _sut.Validate(new AttachProofOfDeliveryCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "http://res.cloudinary.com/demo/image/upload/pod.jpg"));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_NonCloudinaryProofUrl_Fails()
    {
        var result = _sut.Validate(new AttachProofOfDeliveryCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "https://evil.com/x.jpg"));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_CloudinaryHttpsProofUrl_Passes()
    {
        var result = _sut.Validate(new AttachProofOfDeliveryCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "https://res.cloudinary.com/demo/image/upload/x.jpg"));

        result.IsValid.Should().BeTrue();
    }
}

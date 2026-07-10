using FluentAssertions;
using FreshFlow.Auth.Application.Commands.CreateLicenseUploadSignature;
using FreshFlow.SharedKernel.Application;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class CreateLicenseUploadSignatureCommandHandlerTests
{
    private readonly ICloudinarySignatureService _signer = Substitute.For<ICloudinarySignatureService>();
    private readonly CreateLicenseUploadSignatureCommandHandler _sut;

    public CreateLicenseUploadSignatureCommandHandlerTests() =>
        _sut = new CreateLicenseUploadSignatureCommandHandler(_signer);

    [Fact]
    public async Task Handle_AlwaysSignsServerConstantLicenseFolderAsync()
    {
        _signer.Sign(Arg.Any<CloudinarySignatureRequest>())
            .Returns(new CloudinarySignatureResult("sig", 1_700_000_000, "key", "cloud", "freshflow/licenses"));

        await _sut.Handle(new CreateLicenseUploadSignatureCommand(), default);

        _signer.Received(1).Sign(Arg.Is<CloudinarySignatureRequest>(
            r => r.Folder == "freshflow/licenses" && r.Extra == null));
    }

    [Fact]
    public async Task Handle_ReturnsSignatureResultMappedToResponseAsync()
    {
        _signer.Sign(Arg.Any<CloudinarySignatureRequest>())
            .Returns(new CloudinarySignatureResult(
                "sig-abc", 1_700_000_000, "key-123", "cloud-x", "freshflow/licenses"));

        var result = await _sut.Handle(new CreateLicenseUploadSignatureCommand(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Signature.Should().Be("sig-abc");
        result.Value.Timestamp.Should().Be(1_700_000_000);
        result.Value.ApiKey.Should().Be("key-123");
        result.Value.CloudName.Should().Be("cloud-x");
        result.Value.Folder.Should().Be("freshflow/licenses");
    }
}

using FluentAssertions;
using FreshFlow.Auth.Application.Commands.CreateAvatarUploadSignature;
using FreshFlow.SharedKernel.Application;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class CreateAvatarUploadSignatureCommandHandlerTests
{
    private readonly ICloudinarySignatureService _signer = Substitute.For<ICloudinarySignatureService>();
    private readonly CreateAvatarUploadSignatureCommandHandler _sut;

    public CreateAvatarUploadSignatureCommandHandlerTests() =>
        _sut = new CreateAvatarUploadSignatureCommandHandler(_signer);

    [Fact]
    public async Task Handle_AlwaysSignsServerConstantAvatarFolderAsync()
    {
        _signer.Sign(Arg.Any<CloudinarySignatureRequest>())
            .Returns(new CloudinarySignatureResult("sig", 1_700_000_000, "key", "cloud", "freshflow/avatars"));

        await _sut.Handle(new CreateAvatarUploadSignatureCommand(), default);

        _signer.Received(1).Sign(Arg.Is<CloudinarySignatureRequest>(
            r => r.Folder == "freshflow/avatars" && r.Extra == null));
    }

    [Fact]
    public async Task Handle_ReturnsSignatureResultMappedToResponseAsync()
    {
        _signer.Sign(Arg.Any<CloudinarySignatureRequest>())
            .Returns(new CloudinarySignatureResult("sig-abc", 1_700_000_000, "key-123", "cloud-x", "freshflow/avatars"));

        var result = await _sut.Handle(new CreateAvatarUploadSignatureCommand(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Signature.Should().Be("sig-abc");
        result.Value.Timestamp.Should().Be(1_700_000_000);
        result.Value.ApiKey.Should().Be("key-123");
        result.Value.CloudName.Should().Be("cloud-x");
        result.Value.Folder.Should().Be("freshflow/avatars");
    }
}
